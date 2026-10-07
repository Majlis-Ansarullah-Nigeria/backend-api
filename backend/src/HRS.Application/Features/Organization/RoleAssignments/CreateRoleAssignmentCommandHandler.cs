using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Identity.Invitations;
using HRS.Domain.Access;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Organization.RoleAssignments;

public sealed class CreateRoleAssignmentCommandHandler(
    IAppDbContext db,
    IAccessPolicy accessPolicy,
    IIdentityService identityService,
    ISender sender,
    IClock clock) : IRequestHandler<CreateRoleAssignmentCommand, Result<CreateRoleAssignmentResult>>
{
    public async ValueTask<Result<CreateRoleAssignmentResult>> Handle(
        CreateRoleAssignmentCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsOrganizationAdminAsync(request.ActorUserId, cancellationToken))
        {
            return Result.Failure<CreateRoleAssignmentResult>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat or an Admin can manage the organization."));
        }

        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == request.UnitId, cancellationToken);

        if (unit is null)
        {
            return Result.Failure<CreateRoleAssignmentResult>(Error.Validation("UNIT_NOT_FOUND", "Unknown unit."));
        }

        if (unit.IsArchived)
        {
            return Result.Failure<CreateRoleAssignmentResult>(
                Error.Conflict("UNIT_ARCHIVED", "Can't assign a role on an archived unit."));
        }

        if (request.Role == RoleName.Delegate && request.DelegateScope is null)
        {
            return Result.Failure<CreateRoleAssignmentResult>(
                Error.Validation("DELEGATE_SCOPE_REQUIRED", "Choose a delegate scope."));
        }

        var now = clock.UtcNow;

        if (request.Role == RoleName.Delegate)
        {
            // Inline the active-window check rather than calling RoleAssignment.IsActive() —
            // that method isn't translatable by EF (same lesson as the organization tree, M2.2).
            var activeDelegateCount = await db.RoleAssignments
                .Where(a => a.UnitId == request.UnitId && a.Role == RoleName.Delegate
                    && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
                .CountAsync(cancellationToken);

            if (activeDelegateCount >= 2)
            {
                return Result.Failure<CreateRoleAssignmentResult>(
                    Error.Conflict("DELEGATE_LIMIT_REACHED", "This unit already has 2 active delegates (FR-AC-004)."));
            }
        }

        if (request.Role == RoleName.UnitHead)
        {
            // Assigning a head is the deliberate replace action: end the current one, if any.
            var currentHead = await db.RoleAssignments
                .Where(a => a.UnitId == request.UnitId && a.Role == RoleName.UnitHead
                    && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
                .FirstOrDefaultAsync(cancellationToken);

            currentHead?.End(now);
        }

        var existingUserId = await identityService.FindUserIdByEmailAsync(request.Email, cancellationToken);

        if (existingUserId is { } userId)
        {
            var assignment = new RoleAssignment(
                Guid.CreateVersion7(), userId, request.UnitId, request.Role, request.DelegateScope,
                request.StartsAt, request.EndsAt, appointedBy: request.ActorUserId);

            db.RoleAssignments.Add(assignment);
            await db.SaveChangesAsync(cancellationToken);

            return Result.Success(new CreateRoleAssignmentResult(assignment.Id, Invited: false));
        }

        var invited = await sender.Send(
            new CreateInvitationCommand(
                request.Email, request.Name, request.UnitId, request.Role, request.DelegateScope,
                request.ActorUserId, request.StartsAt, request.EndsAt),
            cancellationToken);

        if (invited.IsFailure)
        {
            return Result.Failure<CreateRoleAssignmentResult>(invited.Error);
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new CreateRoleAssignmentResult(null, Invited: true));
    }
}
