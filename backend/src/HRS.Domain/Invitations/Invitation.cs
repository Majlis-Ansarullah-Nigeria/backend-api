using HRS.Domain.Access;

namespace HRS.Domain.Invitations;

/// <summary>
/// A pending invite to join HRS (FR-IAM-002). Stores only a hash of the invite token.
/// <see cref="PendingHeadOfUnitId"/> (plus <see cref="PendingRole"/>/<see cref="PendingDelegateScope"/>)
/// is the one exception to "carries no unit/role reference" — bulk import (M2.3, FR-ORG-003)
/// and the interactive "assign a role" flow (M2.4, FR-ORG-004/FR-AC-004) invite people who
/// don't have accounts yet, and the promised RoleAssignment has to land automatically when
/// they accept, not require a second manual step. <see cref="PendingRole"/> defaults to
/// UnitHead when only the unit is set, so M2.3's original call site needs no changes.
/// </summary>
public sealed class Invitation
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);

    private Invitation()
    {
    }

    private Invitation(
        Guid id, string email, string name, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt,
        Guid? pendingHeadOfUnitId, RoleName? pendingRole, DelegateScope? pendingDelegateScope, Guid? appointedBy,
        DateTimeOffset? pendingStartsAt, DateTimeOffset? pendingEndsAt)
    {
        Id = id;
        Email = email;
        Name = name;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        PendingHeadOfUnitId = pendingHeadOfUnitId;
        PendingRole = pendingRole;
        PendingDelegateScope = pendingDelegateScope;
        AppointedBy = appointedBy;
        PendingStartsAt = pendingStartsAt;
        PendingEndsAt = pendingEndsAt;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>When set, accepting this invitation also grants a role on this unit (FR-ORG-003/FR-ORG-004/FR-AC-004).</summary>
    public Guid? PendingHeadOfUnitId { get; private set; }

    /// <summary>The role to grant on acceptance. Null means UnitHead, for M2.3's original call site.</summary>
    public RoleName? PendingRole { get; private set; }

    /// <summary>Required (and only meaningful) when <see cref="PendingRole"/> is Delegate.</summary>
    public DelegateScope? PendingDelegateScope { get; private set; }

    /// <summary>Who assigned the pending role, carried onto the RoleAssignment created on acceptance.</summary>
    public Guid? AppointedBy { get; private set; }

    /// <summary>The term's start date. Null means "when they accept" — M2.3's bulk import and M2.4's inline terms both use this.</summary>
    public DateTimeOffset? PendingStartsAt { get; private set; }

    /// <summary>The term's end date, if the admin set one when inviting (FR-ORG-004). Null means open-ended.</summary>
    public DateTimeOffset? PendingEndsAt { get; private set; }

    public static Invitation Create(
        Guid id, string email, string name, string tokenHash, DateTimeOffset now,
        Guid? pendingHeadOfUnitId = null, RoleName? pendingRole = null, DelegateScope? pendingDelegateScope = null,
        Guid? appointedBy = null, DateTimeOffset? pendingStartsAt = null, DateTimeOffset? pendingEndsAt = null) =>
        new(
            id, email, name, tokenHash, now, now.Add(DefaultLifetime), pendingHeadOfUnitId, pendingRole,
            pendingDelegateScope, appointedBy, pendingStartsAt, pendingEndsAt);

    public bool IsValid(DateTimeOffset now) => AcceptedAt is null && RevokedAt is null && now < ExpiresAt;

    public void Accept(DateTimeOffset now) => AcceptedAt = now;

    public void Revoke(DateTimeOffset now) => RevokedAt = now;
}
