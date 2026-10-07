using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Unit = HRS.Domain.Organization.Unit;

namespace HRS.Application.Features.Admin.Users;

/// <summary>
/// The whole users screen (FR-ADM-002) in one shot — real accounts plus still-pending
/// invitations, combined. Same "load it all, filter client-side" shape as the organization
/// tree (M2.2): reasonable at this org's scale, and search spans unit names too, which would
/// otherwise need a cross-store server-side search across Identity and RoleAssignments.
/// </summary>
public sealed record GetUsersQuery(Guid ActorUserId) : IRequest<Result<IReadOnlyList<UserListItem>>>;

public enum UserAccountStatus
{
    Active,
    Invited,
    Deactivated,
}

public sealed record UserRoleAssignmentItem(
    Guid Id,
    RoleName Role,
    DelegateScope? DelegateScope,
    Guid UnitId,
    string UnitName,
    string UnitDisplayCode,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    bool IsActive);

public sealed record UserListItem(
    Guid? UserId,
    Guid? InvitationId,
    string Name,
    string Email,
    DateTimeOffset? LastLoginAt,
    bool TwoFactorEnabled,
    UserAccountStatus Status,
    IReadOnlyList<UserRoleAssignmentItem> RoleAssignments);

public sealed class GetUsersQueryHandler(IAppDbContext db, IIdentityService identityService, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GetUsersQuery, Result<IReadOnlyList<UserListItem>>>
{
    public async ValueTask<Result<IReadOnlyList<UserListItem>>> Handle(
        GetUsersQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsOrganizationAdminAsync(request.ActorUserId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<UserListItem>>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat or an Admin can manage users."));
        }

        var now = clock.UtcNow;

        var units = await db.Units.AsNoTracking().ToListAsync(cancellationToken);
        var unitById = units.ToDictionary(u => u.Id, u => u);

        var assignments = await db.RoleAssignments.AsNoTracking().ToListAsync(cancellationToken);
        var assignmentsByUser = assignments
            .Where(a => unitById.ContainsKey(a.UnitId))
            .GroupBy(a => a.UserId)
            .ToDictionary(g => g.Key, g => g.Select(a => ToItem(a.Id, a.Role, a.DelegateScope, unitById[a.UnitId], a.StartsAt, a.EndsAt, now)).ToList());

        var accounts = await identityService.GetAllUsersAsync(cancellationToken);

        var items = accounts
            .Select(account => new UserListItem(
                account.Id,
                null,
                account.Name,
                account.Email,
                account.LastLoginAt,
                account.TwoFactorEnabled,
                account.DeactivatedAt is not null ? UserAccountStatus.Deactivated : UserAccountStatus.Active,
                assignmentsByUser.TryGetValue(account.Id, out var list) ? list : []))
            .ToList();

        var pendingInvitations = await db.Invitations
            .AsNoTracking()
            .Where(i => i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var invitation in pendingInvitations)
        {
            IReadOnlyList<UserRoleAssignmentItem> pendingRole =
                invitation.PendingHeadOfUnitId is { } unitId && unitById.TryGetValue(unitId, out var unit)
                    ? [ToItem(invitation.Id, invitation.PendingRole ?? RoleName.UnitHead, invitation.PendingDelegateScope, unit, invitation.PendingStartsAt ?? invitation.CreatedAt, invitation.PendingEndsAt, now, isActive: false)]
                    : [];

            items.Add(new UserListItem(
                null, invitation.Id, invitation.Name, invitation.Email, null, false, UserAccountStatus.Invited, pendingRole));
        }

        return Result.Success<IReadOnlyList<UserListItem>>(items);
    }

    private static UserRoleAssignmentItem ToItem(
        Guid id, RoleName role, DelegateScope? delegateScope, Unit unit,
        DateTimeOffset startsAt, DateTimeOffset? endsAt, DateTimeOffset now, bool? isActive = null) =>
        new(
            id, role, delegateScope, unit.Id, unit.Name, unit.DisplayCode(), startsAt, endsAt,
            isActive ?? (startsAt <= now && (endsAt is null || endsAt > now)));
}
