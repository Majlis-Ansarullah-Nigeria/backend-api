using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Review.Workload;

public sealed record StaffWorkload(Guid UserId, string Name, int AssignedCount);

/// <summary>FR-REV-004's workload view. Scoped to reports still "in play" — Submitted, InReview,
/// Returned — not Acknowledged, since that work is done and would otherwise inflate a staff
/// member's count forever. Staff doubles as the "who can this be assigned to" list for the
/// inbox's own Assign action, so the frontend doesn't need a second endpoint for that.</summary>
public sealed record GetWorkloadQuery(Guid UserId) : IRequest<Result<WorkloadView>>;

public sealed record WorkloadView(IReadOnlyList<StaffWorkload> Staff, int UnassignedCount);

public sealed class GetWorkloadQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IIdentityService identityService, IClock clock)
    : IRequestHandler<GetWorkloadQuery, Result<WorkloadView>>
{
    private static readonly HashSet<ReportInstanceStatus> InPlayStatuses =
        [ReportInstanceStatus.Submitted, ReportInstanceStatus.InReview, ReportInstanceStatus.Returned];

    public async ValueTask<Result<WorkloadView>> Handle(GetWorkloadQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure<WorkloadView>(Error.Forbidden("FORBIDDEN", "Only the Secretariat has a workload view."));
        }

        var now = clock.UtcNow;
        var staffUserIds = await db.RoleAssignments.AsNoTracking()
            .Where(a => a.Role == RoleName.Secretariat && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .Select(a => a.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var inPlay = await db.ReportInstances.AsNoTracking()
            .Where(i => InPlayStatuses.Contains(i.Status))
            .Select(i => i.AssignedToUserId)
            .ToListAsync(cancellationToken);

        var countsByUserId = inPlay.Where(id => id is not null).GroupBy(id => id!.Value).ToDictionary(g => g.Key, g => g.Count());
        var unassignedCount = inPlay.Count(id => id is null);

        var namesByUserId = await identityService.GetUserNamesAsync(staffUserIds, cancellationToken);
        IReadOnlyList<StaffWorkload> staff = staffUserIds
            .Select(id => new StaffWorkload(id, namesByUserId.GetValueOrDefault(id, "Unknown"), countsByUserId.GetValueOrDefault(id, 0)))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Result.Success(new WorkloadView(staff, unassignedCount));
    }
}
