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

        return new AuthenticatedUser(user.Id, user.Name, user.Email!, user.ActiveRoleAssignmentId);
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
        return user is null ? null : new AuthenticatedUser(user.Id, user.Name, user.Email!, user.ActiveRoleAssignmentId);
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

        return new AuthenticatedUser(user.Id, user.Name, user.Email!, user.ActiveRoleAssignmentId);
    }

    public async Task<bool> EmailIsRegisteredAsync(string email, CancellationToken cancellationToken) =>
        await userManager.FindByEmailAsync(email) is not null;

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

    private async Task<ApplicationUser?> FindUserAsync(string identifier, CancellationToken cancellationToken) =>
        await userManager.FindByEmailAsync(identifier)
            ?? await userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == identifier, cancellationToken);

    private static string OtpKey(Guid userId) => $"otp:{userId}";

    private static string NormalizeIdentifier(string identifier) => identifier.Trim().ToLowerInvariant();

    private static string GenerateOtpCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    private async Task<AuthTokens> CreateSessionAsync(
        ApplicationUser user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var accessToken = tokenGenerator.Generate(user.Id, user.Email!, user.Name);
        var refreshTokenValue = GenerateRefreshTokenValue();
        var now = clock.UtcNow;
        var expiresAt = now.Add(_authOptions.RefreshTokenLifetime);

        var session = new UserSession(
            Guid.CreateVersion7(),
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

    private static Error AccountLocked =>
        Error.Forbidden("ACCOUNT_LOCKED", "Too many failed attempts. Try again in 15 minutes.");

    private static Error InvalidRefreshToken =>
        Error.Unauthenticated("INVALID_REFRESH_TOKEN", "Refresh token is invalid or expired.");

    private static string GenerateRefreshTokenValue() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
