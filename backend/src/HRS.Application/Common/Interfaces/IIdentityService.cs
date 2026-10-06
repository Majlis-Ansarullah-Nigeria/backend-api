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

    /// <summary>Whether an account already exists for this email (FR-IAM-002 invite dedup).</summary>
    Task<bool> EmailIsRegisteredAsync(string email, CancellationToken cancellationToken);

    /// <summary>The existing user's id for this email, or null if no account is registered (bulk import, FR-ORG-003).</summary>
    Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new account with a password already set (used by invitation accept —
    /// FR-IAM-002). Email is pre-confirmed since possession of the invite link proves it.
    /// Fails with Conflict on a duplicate email or Validation if the password fails Identity's
    /// policy (length, required digit, common-password denylist).
    /// </summary>
    Task<Result<AuthenticatedUser>> CreateUserAsync(
        string email,
        string name,
        string? phone,
        string password,
        CancellationToken cancellationToken);

    /// <summary>Sets which role assignment the unit switcher shows (FR-AC-002). Account-wide.</summary>
    Task SetActiveRoleAssignmentAsync(Guid userId, Guid roleAssignmentId, CancellationToken cancellationToken);

    /// <summary>
    /// Mints a short-lived, single-use challenge for a user who passed their first factor but
    /// has TOTP enabled (FR-IAM-003). The caller still has to present it to
    /// <see cref="VerifyMfaChallengeAsync"/> with a code before a session is issued.
    /// </summary>
    Task<string> CreateMfaChallengeAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies a TOTP code (falling back to a recovery code) against the user behind a
    /// challenge token, applying the same lockout policy as password/OTP login for wrong
    /// attempts. The caller still calls <see cref="IssueTokensAsync"/> to start the session.
    /// </summary>
    Task<Result<AuthenticatedUser>> VerifyMfaChallengeAsync(
        string challengeToken,
        string code,
        CancellationToken cancellationToken);

    /// <summary>Generates (or regenerates) a TOTP secret, not yet enabled until confirmed.</summary>
    Task<TotpEnrollment> BeginTotpEnrollmentAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Verifies the just-issued secret and turns MFA on, returning one-time recovery codes.</summary>
    Task<Result<IReadOnlyList<string>>> ConfirmTotpEnrollmentAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken);

    /// <summary>Turns MFA off. Callers must check FR-IAM-003's mandatory-role rule first — this doesn't.</summary>
    Task DisableTotpAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Every active (unrevoked, unexpired) session for the user, most recent first.</summary>
    Task<IReadOnlyList<SessionSummary>> GetActiveSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken);

    /// <summary>Revokes one session. Rejects revoking the caller's own current session — use logout instead.</summary>
    Task<Result> RevokeSessionByIdAsync(
        Guid userId,
        Guid sessionId,
        Guid currentSessionId,
        CancellationToken cancellationToken);

    /// <summary>"Sign out other devices" (FR-IAM-006) — every active session except the current one.</summary>
    Task RevokeOtherSessionsAsync(Guid userId, Guid currentSessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Display names for a set of users, keyed by Id. Missing ids (e.g. a deleted account) are
    /// simply absent from the result. Used to resolve unit heads for the organization tree
    /// (M2.2) without exposing ApplicationUser (an Infrastructure type) to Application.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}

public sealed record AuthenticatedUser(
    Guid Id,
    string Name,
    string Email,
    Guid? ActiveRoleAssignmentId = null,
    bool TwoFactorEnabled = false);

public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record TotpEnrollment(string SharedKey, string AuthenticatorUri);

public sealed record SessionSummary(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string? CreatedByIp,
    string? UserAgent,
    bool IsCurrent);
