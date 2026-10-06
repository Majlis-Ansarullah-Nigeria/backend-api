namespace HRS.Domain.Invitations;

/// <summary>
/// A pending invite to join HRS (FR-IAM-002). Stores only a hash of the invite token.
/// <see cref="PendingHeadOfUnitId"/> is the one exception to "carries no unit/role
/// reference" — bulk import (M2.3, FR-ORG-003) invites heads who don't have accounts yet,
/// and the promised RoleAssignment has to land automatically when they accept, not require
/// a second manual step. M2.4's interactive "assign a head" sets this the same way.
/// </summary>
public sealed class Invitation
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);

    private Invitation()
    {
    }

    private Invitation(
        Guid id, string email, string name, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt,
        Guid? pendingHeadOfUnitId)
    {
        Id = id;
        Email = email;
        Name = name;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        PendingHeadOfUnitId = pendingHeadOfUnitId;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>When set, accepting this invitation also grants UnitHead on this unit (FR-ORG-003/FR-ORG-004).</summary>
    public Guid? PendingHeadOfUnitId { get; private set; }

    public static Invitation Create(
        Guid id, string email, string name, string tokenHash, DateTimeOffset now, Guid? pendingHeadOfUnitId = null) =>
        new(id, email, name, tokenHash, now, now.Add(DefaultLifetime), pendingHeadOfUnitId);

    public bool IsValid(DateTimeOffset now) => AcceptedAt is null && RevokedAt is null && now < ExpiresAt;

    public void Accept(DateTimeOffset now) => AcceptedAt = now;

    public void Revoke(DateTimeOffset now) => RevokedAt = now;
}
