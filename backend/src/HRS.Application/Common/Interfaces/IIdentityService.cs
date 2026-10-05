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

    /// <summary>
    /// Generates and stores a one-time code (FR-IAM-001) and queues it for email delivery if the
    /// identifier matches an account. Always succeeds to the caller regardless of whether a match
    /// was found, so the endpoint never reveals account existence. Per-identifier request
    /// throttling (NFR-SEC-04) returns a RateLimited failure instead.
    /// </summary>
    Task<Result> RequestOtpAsync(string identifier, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies a one-time code (single use), applying the same lockout policy as password
    /// login (FR-IAM-005) for wrong attempts. Updates LastLoginAt on success, mirroring
    /// <see cref="ValidateCredentialsAsync"/> — the caller still calls
    /// <see cref="IssueTokensAsync"/> to start the session.
    /// </summary>
    Task<Result<AuthenticatedUser>> VerifyOtpAsync(
        string identifier,
        string code,
        CancellationToken cancellationToken);
}

public sealed record AuthenticatedUser(Guid Id, string Name, string Email);

public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
