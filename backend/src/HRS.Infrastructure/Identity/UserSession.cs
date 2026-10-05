namespace HRS.Infrastructure.Identity;

/// <summary>
/// A rotating refresh-token session. Stores only a hash of the refresh token value.
/// Doubles as the backing store for the session list (FR-IAM-006, M1.5).
/// </summary>
public sealed class UserSession
{
    private UserSession()
    {
    }

    public UserSession(
        Guid id,
        Guid userId,
        string refreshTokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        string? createdByIp,
        string? userAgent)
    {
        Id = id;
        UserId = userId;
        RefreshTokenHash = refreshTokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
        UserAgent = userAgent;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string RefreshTokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public string? CreatedByIp { get; private set; }

    public string? UserAgent { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    public void Revoke(DateTimeOffset at, string? replacedByTokenHash = null)
    {
        RevokedAt = at;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
