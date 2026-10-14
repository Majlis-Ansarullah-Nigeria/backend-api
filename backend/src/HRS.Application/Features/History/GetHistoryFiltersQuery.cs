using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.History;

public sealed record HistoryReportTypeOption(Guid Id, string Name);

public sealed record HistoryPeriodOption(Guid WindowId, string PeriodLabel, DateTimeOffset OpensAt);

public sealed record HistoryFilterOptions(IReadOnlyList<HistoryReportTypeOption> ReportTypes, IReadOnlyList<HistoryPeriodOption> Periods);

/// <summary>Bundled option lists for the filter row's selects, scoped the same way the list
/// itself is — never offering a type/period the caller has no reports under.</summary>
public sealed record GetHistoryFiltersQuery(Guid UserId) : IRequest<Result<HistoryFilterOptions>>;

public sealed class GetHistoryFiltersQueryHandler(IAppDbContext db, IClock clock) : IRequestHandler<GetHistoryFiltersQuery, Result<HistoryFilterOptions>>
{
    public async ValueTask<Result<HistoryFilterOptions>> Handle(GetHistoryFiltersQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var scope = await HistoryScopeResolver.ResolveAsync(db, request.UserId, now, cancellationToken);
        if (scope.UnitIds.Count == 0)
        {
            return Result.Success(new HistoryFilterOptions([], []));
        }

        var unitIds = scope.UnitIds.ToList();
        var instances = await db.ReportInstances.AsNoTracking()
            .Where(i => unitIds.Contains(i.UnitId))
            .Select(i => new { i.UnitId, i.WindowId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var units = await db.Units.AsNoTracking()
            .Where(u => unitIds.Contains(u.Id))
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { u.Id, LevelCode = l.Code })
            .ToListAsync(cancellationToken);
        var levelCodeByUnit = units.ToDictionary(u => u.Id, u => u.LevelCode);

        var allWindows = await db.SubmissionWindows.AsNoTracking()
            .Where(w => instances.Select(i => i.WindowId).Contains(w.Id))
            .ToListAsync(cancellationToken);
        var windowById = allWindows.ToDictionary(w => w.Id);

        var reportTypes = await db.ReportTypes.AsNoTracking()
            .Where(r => allWindows.Select(w => w.ReportTypeId).Contains(r.Id))
            .ToListAsync(cancellationToken);
        var reportTypeById = reportTypes.ToDictionary(r => r.Id);

        // Same per-(unit, report type) observability gate as GetHistoryQuery: scope.ObservedUnitIds
        // only says this unit's level is observable by *some* report type, not this one.
        var visibleWindowIds = new HashSet<Guid>();
        foreach (var instance in instances)
        {
            if (!windowById.TryGetValue(instance.WindowId, out var window)
                || !reportTypeById.TryGetValue(window.ReportTypeId, out var reportType)
                || !levelCodeByUnit.TryGetValue(instance.UnitId, out var levelCode))
            {
                continue;
            }

            if (scope.ObservedUnitIds.Contains(instance.UnitId)
                && (reportType.ObserverDepth < 1 || !reportType.ApplicableLevels.Contains(levelCode)))
            {
                continue;
            }

            visibleWindowIds.Add(window.Id);
        }

        var windows = allWindows.Where(w => visibleWindowIds.Contains(w.Id)).OrderBy(w => w.OpensAt).ToList();

        var reportTypeOptions = windows
            .Select(w => w.ReportTypeId)
            .Distinct()
            .Where(reportTypeById.ContainsKey)
            .Select(id => new HistoryReportTypeOption(id, reportTypeById[id].Name))
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .ToList();

        var periods = windows.Select(w => new HistoryPeriodOption(w.Id, w.PeriodLabel, w.OpensAt)).ToList();

        return Result.Success(new HistoryFilterOptions(reportTypeOptions, periods));
    }
}
