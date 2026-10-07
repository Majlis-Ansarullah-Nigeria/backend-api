using HRS.Domain.Access;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Organization.RoleAssignments;

/// <summary>
/// Assigns a role to a unit (FR-ORG-004: heads; FR-AC-004: delegates), used both by the
/// Organization tree's "assign a head" panel and the Users screen's "add role" action.
/// Resolves an existing user by email, or invites a new one — the promised RoleAssignment
/// lands automatically when they accept (see Invitation.PendingRole).
/// </summary>
public sealed record CreateRoleAssignmentCommand(
    Guid ActorUserId,
    Guid UnitId,
    string Email,
    string Name,
    RoleName Role,
    DelegateScope? DelegateScope,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt) : IRequest<Result<CreateRoleAssignmentResult>>;

public sealed record CreateRoleAssignmentResult(Guid? RoleAssignmentId, bool Invited);
