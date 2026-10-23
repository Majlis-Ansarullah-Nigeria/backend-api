using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Review.Inbox;

/// <summary>FR-REV-004's "assign reports to Secretariat staff members" — current assignee only
/// (ReportInstance.AssignedToUserId's own doc comment), not a history. AssignedToUserId null
/// clears the assignment. No notification: FR-NOT-002's event list has no "assigned" event, same
/// reasoning as SetReportFlaggedCommand.</summary>
public sealed record AssignReportCommand(Guid UserId, Guid ReportId, Guid? AssignedToUserId) : IRequest<Result>;

public sealed class AssignReportCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IAuditLog auditLog)
    : IRequestHandler<AssignReportCommand, Result>
{
    public async ValueTask<Result> Handle(AssignReportCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "Only the Secretariat has an inbox."));
        }

        var instance = await db.ReportInstances.FirstOrDefaultAsync(i => i.Id == request.ReportId, cancellationToken);
        if (instance is null || instance.Status is ReportInstanceStatus.NotStarted or ReportInstanceStatus.Draft or ReportInstanceStatus.Missed)
        {
            return Result.Failure(Error.NotFound("REPORT_NOT_FOUND", "That report doesn't exist."));
        }

        if (request.AssignedToUserId is { } assignedToUserId)
        {
            var now = clock.UtcNow;
            var isActiveSecretariatStaff = await db.RoleAssignments.AsNoTracking().AnyAsync(
                a => a.UserId == assignedToUserId && a.Role == RoleName.Secretariat
                    && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now),
                cancellationToken);
            if (!isActiveSecretariatStaff)
            {
                return Result.Failure(Error.Validation("NOT_SECRETARIAT_STAFF", "Reports can only be assigned to a current Secretariat staff member."));
            }
        }

        var previousAssignee = instance.AssignedToUserId;
        instance.AssignTo(request.AssignedToUserId);
        auditLog.Add(new AuditEntry(
            AuditActions.ReportAssigned, request.UserId, "report", instance.Id.ToString(),
            await AuditDescriptions.ReportAsync(db, instance.WindowId, instance.UnitId, cancellationToken), instance.UnitId,
            Before: new { AssignedTo = previousAssignee }, After: new { AssignedTo = request.AssignedToUserId }));
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
