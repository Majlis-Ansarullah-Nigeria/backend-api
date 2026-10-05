using HRS.Domain.Common;

namespace HRS.Application.Common.Interfaces;

public interface IIdentityService
{
    /// <summary>
    /// Checks the identifier (email or phone) and password, applying ASP.NET Core
    /// Identity's lockout policy (FR-IAM-005). Updates LastLoginAt on success.
    /// </summary>
    Task<Result<AuthenticatedUser>> ValidateCredentialsAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken);

    /// <summary>Issues a new access/refresh token pair and persists the session.</summary>
    Task<AuthTokens> IssueTokensAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rotates a refresh token: validates it, revokes it, and issues a new pair.
    /// If a already-revoked token is presented (reuse), every active session for
    /// that user is revoked and the request is rejected.
    /// </summary>
    Task<Result<AuthTokens>> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken);

    /// <summary>Revokes the session backing the given refresh token (logout).</summary>
    Task RevokeSessionAsync(string refreshToken, CancellationToken cancellationToken);

    Task<AuthenticatedUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record AuthenticatedUser(Guid Id, string Name, string Email);

public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
