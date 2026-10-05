using System.Security.Cryptography;
using System.Text;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Infrastructure.Auth;
using HRS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRS.Infrastructure.Identity;

public sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    AppDbContext db,
    IAccessTokenGenerator tokenGenerator,
    IClock clock,
    IOptions<AuthOptions> authOptions) : IIdentityService
{
    private readonly AuthOptions _authOptions = authOptions.Value;

    public async Task<Result<AuthenticatedUser>> ValidateCredentialsAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(identifier)
            ?? await userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == identifier, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AuthenticatedUser>(InvalidCredentials);
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            return Result.Failure<AuthenticatedUser>(
                Error.Forbidden("ACCOUNT_LOCKED", "Too many failed attempts. Try again in 15 minutes."));
        }

        if (!signInResult.Succeeded)
        {
            return Result.Failure<AuthenticatedUser>(InvalidCredentials);
        }

        user.LastLoginAt = clock.UtcNow;
        await userManager.UpdateAsync(user);

        return new AuthenticatedUser(user.Id, user.Name, user.Email!);
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
        return user is null ? null : new AuthenticatedUser(user.Id, user.Name, user.Email!);
    }

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

    private static Error InvalidRefreshToken =>
        Error.Unauthenticated("INVALID_REFRESH_TOKEN", "Refresh token is invalid or expired.");

    private static string GenerateRefreshTokenValue() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
