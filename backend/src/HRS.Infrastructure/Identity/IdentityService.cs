using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Infrastructure.Auth;
using HRS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HRS.Infrastructure.Identity;

public sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    AppDbContext db,
    IConnectionMultiplexer redis,
    IEmailSender emailSender,
    IAccessTokenGenerator tokenGenerator,
    IClock clock,
    IOptions<AuthOptions> authOptions) : IIdentityService
{
    private static readonly TimeSpan OtpCodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan OtpRequestWindow = TimeSpan.FromMinutes(15);
    private const int OtpMaxRequestsPerWindow = 3;

    private static readonly TimeSpan MfaChallengeLifetime = TimeSpan.FromMinutes(5);
    private const int RecoveryCodeCount = 10;
    private const string TotpIssuer = "HRS Reporting Service";

    private readonly AuthOptions _authOptions = authOptions.Value;

    public async Task<Result<AuthenticatedUser>> ValidateCredentialsAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(identifier, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AuthenticatedUser>(InvalidCredentials);
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            return Result.Failure<AuthenticatedUser>(AccountLocked);
        }

        if (!signInResult.Succeeded)
        {
            return Result.Failure<AuthenticatedUser>(InvalidCredentials);
        }

        user.LastLoginAt = clock.UtcNow;
        await userManager.UpdateAsync(user);

        return new AuthenticatedUser(user.Id, user.Name, user.Email!, user.ActiveRoleAssignmentId, user.TwoFactorEnabled);
    }

    public async Task<AuthTokens> IssueTokensAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User {userId} not found when issuing tokens.");

        return await CreateSessionAsync(user, ipAddress, userAgent, cancellationToken);
    }

    public async Task<Result<AuthTokens>> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var tokenHash = Hash(refreshToken);
        var session = await db.UserSessions.FirstOrDefaultAsync(s => s.RefreshTokenHash == tokenHash, cancellationToken);

        if (session is null)
        {
            return Result.Failure<AuthTokens>(InvalidRefreshToken);
        }

        var now = clock.UtcNow;

        if (session.RevokedAt is not null)
        {
            // Reuse of an already-rotated token: possible theft. Revoke every active
            // session for this user so the legitimate holder has to log in again.
            var activeSessions = await db.UserSessions
                .Where(s => s.UserId == session.UserId && s.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var activeSession in activeSessions)
            {
                activeSession.Revoke(now);
            }

            await db.SaveChangesAsync(cancellationToken);

            return Result.Failure<AuthTokens>(InvalidRefreshToken);
        }

        if (now >= session.ExpiresAt)
        {
            return Result.Failure<AuthTokens>(InvalidRefreshToken);
        }

        var user = await userManager.FindByIdAsync(session.UserId.ToString());

        if (user is null)
        {
            return Result.Failure<AuthTokens>(InvalidRefreshToken);
        }

        var tokens = await CreateSessionAsync(user, ipAddress, userAgent, cancellationToken);
        session.Revoke(now, Hash(tokens.RefreshToken));
        await db.SaveChangesAsync(cancellationToken);

        return tokens;
    }

    public async Task RevokeSessionAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = Hash(refreshToken);
        var session = await db.UserSessions.FirstOrDefaultAsync(s => s.RefreshTokenHash == tokenHash, cancellationToken);

        if (session is not null && session.RevokedAt is null)
        {
            session.Revoke(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<AuthenticatedUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null
            ? null
            : new AuthenticatedUser(user.Id, user.Name, user.Email!, user.ActiveRoleAssignmentId, user.TwoFactorEnabled);
    }

    public async Task<Result> RequestOtpAsync(string identifier, CancellationToken cancellationToken)
    {
        var redisDb = redis.GetDatabase();
        var throttleKey = $"otp-throttle:{NormalizeIdentifier(identifier)}";

        var requestCount = await redisDb.StringIncrementAsync(throttleKey);

        if (requestCount == 1)
        {
            await redisDb.KeyExpireAsync(throttleKey, OtpRequestWindow);
        }

        if (requestCount > OtpMaxRequestsPerWindow)
        {
            var ttl = await redisDb.KeyTimeToLiveAsync(throttleKey) ?? OtpRequestWindow;
            return Result.Failure(Error.RateLimited(
                "OTP_REQUEST_RATE_LIMITED",
                "Too many code requests. Try again later.",
                (int)Math.Ceiling(ttl.TotalSeconds)));
        }

        var user = await FindUserAsync(identifier, cancellationToken);

        if (user is null)
        {
            // No enumeration: behave exactly as if a code had been sent.
            return Result.Success();
        }

        var code = GenerateOtpCode();
        await redisDb.StringSetAsync(OtpKey(user.Id), Hash(code), OtpCodeLifetime);

        await emailSender.QueueAsync(
            new EmailMessage(
                user.Email!,
                "Your HRS sign-in code",
                $"Your sign-in code is {code}. It expires in 10 minutes. If you didn't request this, ignore this email."),
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<AuthenticatedUser>> VerifyOtpAsync(
        string identifier,
        string code,
        CancellationToken cancellationToken)
    {
        var user = await FindUserAsync(identifier, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AuthenticatedUser>(InvalidOtp);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Result.Failure<AuthenticatedUser>(AccountLocked);
        }

        var redisDb = redis.GetDatabase();
        var otpKey = OtpKey(user.Id);
        var storedHash = await redisDb.StringGetAsync(otpKey);

        if (storedHash.IsNull || storedHash != Hash(code))
        {
            await userManager.AccessFailedAsync(user);

            return await userManager.IsLockedOutAsync(user)
                ? Result.Failure<AuthenticatedUser>(AccountLocked)
                : Result.Failure<AuthenticatedUser>(InvalidOtp);
        }

        // Single use: remove the code immediately so it cannot be replayed.
        await redisDb.KeyDeleteAsync(otpKey);
        await userManager.ResetAccessFailedCountAsync(user);

        user.LastLoginAt = clock.UtcNow;
        await userManager.UpdateAsync(user);

        return new AuthenticatedUser(user.Id, user.Name, user.Email!, user.ActiveRoleAssignmentId, user.TwoFactorEnabled);
    }

    public async Task<bool> EmailIsRegisteredAsync(string email, CancellationToken cancellationToken) =>
        await userManager.FindByEmailAsync(email) is not null;

    public async Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken) =>
        (await userManager.FindByEmailAsync(email))?.Id;

    public async Task<Result<AuthenticatedUser>> CreateUserAsync(
        string email,
        string name,
        string? phone,
        string password,
        CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            UserName = email,
            Email = email,
            PhoneNumber = phone,
            // Possession of the invite link already proves the email, per FR-IAM-002.
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var isDuplicate = result.Errors.Any(e =>
                e.Code == nameof(IdentityErrorDescriber.DuplicateEmail)
                || e.Code == nameof(IdentityErrorDescriber.DuplicateUserName));

            return isDuplicate
                ? Result.Failure<AuthenticatedUser>(
                    Error.Conflict("USER_ALREADY_EXISTS", "An account with this email already exists."))
                : Result.Failure<AuthenticatedUser>(
                    Error.Validation("WEAK_PASSWORD", string.Join(" ", result.Errors.Select(e => e.Description))));
        }

        return new AuthenticatedUser(user.Id, user.Name, user.Email!);
    }

    public async Task SetActiveRoleAssignmentAsync(Guid userId, Guid roleAssignmentId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User {userId} not found when setting the active role assignment.");

        user.ActiveRoleAssignmentId = roleAssignmentId;
        await userManager.UpdateAsync(user);
    }

    public async Task<string> CreateMfaChallengeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var challengeToken = GenerateRefreshTokenValue();
        var redisDb = redis.GetDatabase();
        await redisDb.StringSetAsync(MfaChallengeKey(Hash(challengeToken)), userId.ToString(), MfaChallengeLifetime);

        return challengeToken;
    }

    public async Task<Result<AuthenticatedUser>> VerifyMfaChallengeAsync(
        string challengeToken,
        string code,
        CancellationToken cancellationToken)
    {
        var redisDb = redis.GetDatabase();
        var challengeKey = MfaChallengeKey(Hash(challengeToken));
        var userIdValue = await redisDb.StringGetAsync(challengeKey);

        if (userIdValue.IsNull || !Guid.TryParse(userIdValue.ToString(), out var userId))
        {
            return Result.Failure<AuthenticatedUser>(InvalidMfaChallenge);
        }

        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Result.Failure<AuthenticatedUser>(InvalidMfaChallenge);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Result.Failure<AuthenticatedUser>(AccountLocked);
        }

        var isValidCode = await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code)
            || (await userManager.RedeemTwoFactorRecoveryCodeAsync(user, code)).Succeeded;

        if (!isValidCode)
        {
            await userManager.AccessFailedAsync(user);

            return await userManager.IsLockedOutAsync(user)
                ? Result.Failure<AuthenticatedUser>(AccountLocked)
                : Result.Failure<AuthenticatedUser>(InvalidMfaChallenge);
        }

        // Single use: remove the challenge immediately so it cannot be replayed.
        await redisDb.KeyDeleteAsync(challengeKey);
        await userManager.ResetAccessFailedCountAsync(user);

        user.LastLoginAt = clock.UtcNow;
        await userManager.UpdateAsync(user);

        return new AuthenticatedUser(user.Id, user.Name, user.Email!, user.ActiveRoleAssignmentId, user.TwoFactorEnabled);
    }

    public async Task<TotpEnrollment> BeginTotpEnrollmentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User {userId} not found when beginning TOTP enrollment.");

        // Resets (not just reads) the key: a user who abandons setup and starts again gets a
        // fresh secret rather than one that may already be sitting in a stale QR screenshot.
        await userManager.ResetAuthenticatorKeyAsync(user);
        var unformattedKey = await userManager.GetAuthenticatorKeyAsync(user)
            ?? throw new InvalidOperationException("Authenticator key was not generated.");

        return new TotpEnrollment(FormatKeyForDisplay(unformattedKey), BuildAuthenticatorUri(user.Email!, unformattedKey));
    }

    public async Task<Result<IReadOnlyList<string>>> ConfirmTotpEnrollmentAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User {userId} not found when confirming TOTP enrollment.");

        var isValid = await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code);

        if (!isValid)
        {
            return Result.Failure<IReadOnlyList<string>>(InvalidTotpCode);
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);

        return Result.Success<IReadOnlyList<string>>((recoveryCodes ?? []).ToList());
    }

    public async Task DisableTotpAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException($"User {userId} not found when disabling TOTP.");

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
    }

    public async Task<IReadOnlyList<SessionSummary>> GetActiveSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var sessions = await db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return sessions
            .Select(s => new SessionSummary(s.Id, s.CreatedAt, s.ExpiresAt, s.CreatedByIp, s.UserAgent, s.Id == currentSessionId))
            .ToList();
    }

    public async Task<Result> RevokeSessionByIdAsync(
        Guid userId,
        Guid sessionId,
        Guid currentSessionId,
        CancellationToken cancellationToken)
    {
        if (sessionId == currentSessionId)
        {
            return Result.Failure(
                Error.Validation("CANNOT_REVOKE_CURRENT_SESSION", "Use logout to end your current session."));
        }

        var now = clock.UtcNow;
        var session = await db.UserSessions.FirstOrDefaultAsync(
            s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now,
            cancellationToken);

        if (session is null)
        {
            return Result.Failure(Error.NotFound("SESSION_NOT_FOUND", "That session isn't available to you."));
        }

        session.Revoke(now);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task RevokeOtherSessionsAsync(Guid userId, Guid currentSessionId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var sessions = await db.UserSessions
            .Where(s => s.UserId == userId && s.Id != currentSessionId && s.RevokedAt == null && s.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.Revoke(now);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);
    }

    private static string BuildAuthenticatorUri(string email, string unformattedKey) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
            Uri.EscapeDataString(TotpIssuer),
            Uri.EscapeDataString(email),
            unformattedKey);

    /// <summary>Groups the raw base32 secret into 4-character blocks for manual entry (e.g. "ABCD EFGH").</summary>
    private static string FormatKeyForDisplay(string unformattedKey)
    {
        var result = new StringBuilder();
        var position = 0;

        while (position + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(position, 4)).Append(' ');
            position += 4;
        }

        if (position < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(position));
        }

        return result.ToString().ToUpperInvariant();
    }

    private async Task<ApplicationUser?> FindUserAsync(string identifier, CancellationToken cancellationToken) =>
        await userManager.FindByEmailAsync(identifier)
            ?? await userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == identifier, cancellationToken);

    private static string OtpKey(Guid userId) => $"otp:{userId}";

    private static string MfaChallengeKey(string challengeTokenHash) => $"mfa-challenge:{challengeTokenHash}";

    private static string NormalizeIdentifier(string identifier) => identifier.Trim().ToLowerInvariant();

    private static string GenerateOtpCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    private async Task<AuthTokens> CreateSessionAsync(
        ApplicationUser user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var sessionId = Guid.CreateVersion7();
        var accessToken = tokenGenerator.Generate(user.Id, user.Email!, user.Name, sessionId);
        var refreshTokenValue = GenerateRefreshTokenValue();
        var now = clock.UtcNow;
        var expiresAt = now.Add(_authOptions.RefreshTokenLifetime);

        var session = new UserSession(
            sessionId,
            user.Id,
            Hash(refreshTokenValue),
            now,
            expiresAt,
            ipAddress,
            userAgent);

        db.UserSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return new AuthTokens(accessToken.Value, accessToken.ExpiresAt, refreshTokenValue, expiresAt);
    }

    private static Error InvalidCredentials =>
        Error.Unauthenticated("INVALID_CREDENTIALS", "Invalid email/phone or password.");

    private static Error InvalidOtp =>
        Error.Unauthenticated("INVALID_OTP", "That code is invalid or has expired.");

    private static Error InvalidMfaChallenge =>
        Error.Unauthenticated("INVALID_MFA_CHALLENGE", "That code is invalid or the challenge has expired. Log in again.");

    private static Error InvalidTotpCode =>
        Error.Validation("INVALID_TOTP_CODE", "That code is incorrect. Check your authenticator app and try again.");

    private static Error AccountLocked =>
        Error.Forbidden("ACCOUNT_LOCKED", "Too many failed attempts. Try again in 15 minutes.");

    private static Error InvalidRefreshToken =>
        Error.Unauthenticated("INVALID_REFRESH_TOKEN", "Refresh token is invalid or expired.");

    private static string GenerateRefreshTokenValue() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
