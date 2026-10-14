using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.MyReports;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using Microsoft.EntityFrameworkCore;
using Mediator;

namespace HRS.Application.Features.History;

public sealed record HistoryPage(IReadOnlyList<HistoryRow> Rows, int TotalCount, int Page, int PageSize);

/// <summary>Only these six are "past reports" at all (FR-HIS-001) — NotStarted/Draft/Upcoming/
/// Overdue rows have no submission yet, so they belong on "My reports" (FR-SUB-001), not here.</summary>
public static class HistoryStatuses
{
    public static readonly HashSet<ReportLineStatus> All =
    [
        ReportLineStatus.Submitted,
        ReportLineStatus.SubmittedLate,
        ReportLineStatus.InReview,
        ReportLineStatus.Returned,
        ReportLineStatus.Acknowledged,
        ReportLineStatus.Missed,
    ];
}

/// <summary>
/// FR-HIS-001/002: every past report of every unit the caller heads or observes (the same
/// headUnit-&gt;eligible-children scope as GetComplianceGridQuery/ChildReportResolver), filterable
/// by type/period-range/status, searchable across text answers, paginated.
/// </summary>
public sealed record GetHistoryQuery(
    Guid UserId,
    Guid? ReportTypeId,
    Guid? FromWindowId,
    Guid? ToWindowId,
    ReportLineStatus? Status,
    string? SearchTerm,
    int Page,
    int PageSize) : IRequest<Result<HistoryPage>>;

public sealed class GetHistoryQueryHandler(IAppDbContext db, IIdentityService identityService, IClock clock)
    : IRequestHandler<GetHistoryQuery, Result<HistoryPage>>
{
    public async ValueTask<Result<HistoryPage>> Handle(GetHistoryQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 20 : request.PageSize, 1, 100);

        var scope = await HistoryScopeResolver.ResolveAsync(db, request.UserId, now, cancellationToken);
        if (scope.UnitIds.Count == 0)
        {
            return Result.Success(new HistoryPage([], 0, page, pageSize));
        }

        var reportTypes = await db.ReportTypes.AsNoTracking().Where(r => r.ArchivedAt == null).ToListAsync(cancellationToken);
        var reportTypeById = reportTypes.ToDictionary(r => r.Id);
        var reportTypeIds = request.ReportTypeId is { } requestedTypeId
            ? reportTypes.Where(r => r.Id == requestedTypeId).Select(r => r.Id).ToList()
            : reportTypes.Select(r => r.Id).ToList();

        var windows = await db.SubmissionWindows.AsNoTracking()
            .Where(w => reportTypeIds.Contains(w.ReportTypeId))
            .ToListAsync(cancellationToken);
        var windowById = windows.ToDictionary(w => w.Id);

        DateTimeOffset? fromOpensAt = request.FromWindowId is { } fromId && windowById.TryGetValue(fromId, out var fromWindow) ? fromWindow.OpensAt : null;
        DateTimeOffset? toOpensAt = request.ToWindowId is { } toId && windowById.TryGetValue(toId, out var toWindow) ? toWindow.OpensAt : null;

        var windowIds = windows
            .Where(w => (fromOpensAt is null || w.OpensAt >= fromOpensAt) && (toOpensAt is null || w.OpensAt <= toOpensAt))
            .Select(w => w.Id)
            .ToList();

        var unitIds = scope.UnitIds.ToList();
        var instances = await db.ReportInstances.AsNoTracking()
            .Where(i => windowIds.Contains(i.WindowId) && unitIds.Contains(i.UnitId))
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var matchingIds = (await db.SearchReportInstanceAnswersAsync(request.SearchTerm, cancellationToken)).ToHashSet();
            instances = instances.Where(i => matchingIds.Contains(i.Id)).ToList();
        }

        var units = await db.Units.AsNoTracking()
            .Where(u => unitIds.Contains(u.Id))
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { Unit = u, LevelCode = l.Code })
            .ToListAsync(cancellationToken);
        var unitById = units.ToDictionary(u => u.Unit.Id);

        var headAssignments = await db.RoleAssignments.AsNoTracking()
            .Where(a => a.Role == RoleName.UnitHead && unitIds.Contains(a.UnitId))
            .Select(a => new { a.UnitId, a.UserId, a.StartsAt, a.EndsAt })
            .ToListAsync(cancellationToken);
        var currentHeadByUnit = headAssignments
            .Where(a => a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .GroupBy(a => a.UnitId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.StartsAt).First().UserId);
        var everHeadUserIdsByUnit = headAssignments
            .GroupBy(a => a.UnitId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.UserId).ToHashSet());

        var rows = new List<HistoryRow>();
        foreach (var instance in instances)
        {
            if (!windowById.TryGetValue(instance.WindowId, out var window)
                || !reportTypeById.TryGetValue(window.ReportTypeId, out var reportType)
                || !unitById.TryGetValue(instance.UnitId, out var unitEntry))
            {
                continue;
            }

            // scope.ObservedUnitIds only tells us this unit's *level* is observable by *some*
            // report type somewhere — a unit reached only through observation (never headed
            // directly) still needs this specific report type to itself be observable at this
            // unit's level, otherwise a non-observable report type would leak through just
            // because the unit also happens to receive an observable one.
            if (scope.ObservedUnitIds.Contains(instance.UnitId)
                && (reportType.ObserverDepth < 1 || !reportType.ApplicableLevels.Contains(unitEntry.LevelCode)))
            {
                continue;
            }

            var status = MyReportStatusCalculator.Determine(window, instance, unitEntry.LevelCode, instance.UnitId, now);
            if (!HistoryStatuses.All.Contains(status))
            {
                continue;
            }

            if (request.Status is { } requestedStatus && status != requestedStatus)
            {
                continue;
            }

            rows.Add(new HistoryRow(
                instance.Id,
                window.Id,
                reportType.Id,
                reportType.Name,
                instance.UnitId,
                unitEntry.Unit.Name,
                window.PeriodLabel,
                instance.SubmittedAt,
                status,
                SubmittedByLabel: "",
                IsObserved: scope.ObservedUnitIds.Contains(instance.UnitId)));
        }

        var submitterIdByInstanceId = instances.ToDictionary(i => i.Id, i => i.SubmittedByUserId);
        var submitterIds = submitterIdByInstanceId.Values.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        var submitterNames = await identityService.GetUserNamesAsync(submitterIds, cancellationToken);

        rows = rows
            .Select(row =>
            {
                var submittedByUserId = submitterIdByInstanceId.GetValueOrDefault(row.InstanceId);
                var label = ResolveSubmittedByLabel(
                    request.UserId, submittedByUserId, row.UnitId, currentHeadByUnit, everHeadUserIdsByUnit, submitterNames);
                return row with { SubmittedByLabel = label };
            })
            .OrderByDescending(r => r.SubmittedAt)
            .ToList();

        var totalCount = rows.Count;
        var paged = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Result.Success(new HistoryPage(paged, totalCount, page, pageSize));
    }

    private static string ResolveSubmittedByLabel(
        Guid viewerUserId,
        Guid? submittedByUserId,
        Guid unitId,
        Dictionary<Guid, Guid> currentHeadByUnit,
        Dictionary<Guid, HashSet<Guid>> everHeadUserIdsByUnit,
        IReadOnlyDictionary<Guid, string> namesByUserId)
    {
        if (submittedByUserId is not { } submitterId)
        {
            return string.Empty;
        }

        if (submitterId == viewerUserId)
        {
            return "You";
        }

        var isCurrentHead = currentHeadByUnit.TryGetValue(unitId, out var currentHeadId) && currentHeadId == submitterId;
        if (!isCurrentHead && everHeadUserIdsByUnit.TryGetValue(unitId, out var everHeads) && everHeads.Contains(submitterId))
        {
            return "Previous head";
        }

        return namesByUserId.GetValueOrDefault(submitterId, "Unknown");
    }
}
