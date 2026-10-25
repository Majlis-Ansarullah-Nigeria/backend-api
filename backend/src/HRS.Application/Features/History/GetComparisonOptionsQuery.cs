using HRS.Application.Common.Interfaces;
using HRS.Domain.Admin;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.History;

public sealed record ComparisonUnitOption(Guid UnitId, string UnitName, IReadOnlyList<HistoryReportTypeOption> ReportTypes);

/// <summary>The unit and report type pickers on Compare periods (FR-HIS-005): every History-scope
/// unit with the report types it has reports for, after the same observability gate as History.
/// The Secretariat reaches the screen from a specific report instead, so isn't listed here.</summary>
public sealed record GetComparisonOptionsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<ComparisonUnitOption>>>, IRequiresFeature
{
    public FeatureModule Feature => FeatureModule.ComparisonAndVersions;
}

public sealed class GetComparisonOptionsQueryHandler(IAppDbContext db, IClock clock)
    : IRequestHandler<GetComparisonOptionsQuery, Result<IReadOnlyList<ComparisonUnitOption>>>
{
    public async ValueTask<Result<IReadOnlyList<ComparisonUnitOption>>> Handle(GetComparisonOptionsQuery request, CancellationToken cancellationToken)
    {
        var scope = await HistoryScopeResolver.ResolveAsync(db, request.UserId, clock.UtcNow, cancellationToken);
        if (scope.UnitIds.Count == 0)
        {
            return Result.Success<IReadOnlyList<ComparisonUnitOption>>([]);
        }

        var unitIds = scope.UnitIds.ToList();
        var pairs = await db.ReportInstances.AsNoTracking()
            .Where(i => unitIds.Contains(i.UnitId))
            .Join(db.SubmissionWindows.AsNoTracking(), i => i.WindowId, w => w.Id, (i, w) => new { i.UnitId, w.ReportTypeId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var units = await db.Units.AsNoTracking()
            .Where(u => unitIds.Contains(u.Id))
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { u.Id, u.Name, LevelCode = l.Code })
            .ToListAsync(cancellationToken);

        var reportTypeIds = pairs.Select(p => p.ReportTypeId).Distinct().ToList();
        var reportTypeById = await db.ReportTypes.AsNoTracking()
            .Where(r => reportTypeIds.Contains(r.Id) && r.ArchivedAt == null)
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var options = units
            .Select(unit => new ComparisonUnitOption(
                unit.Id,
                unit.Name,
                [.. pairs
                    .Where(p => p.UnitId == unit.Id && reportTypeById.ContainsKey(p.ReportTypeId))
                    .Select(p => reportTypeById[p.ReportTypeId])
                    .Where(r => !scope.ObservedUnitIds.Contains(unit.Id)
                        || (r.ObserverDepth >= 1 && r.ApplicableLevels.Contains(unit.LevelCode)))
                    .Select(r => new HistoryReportTypeOption(r.Id, r.Name))
                    .OrderBy(r => r.Name, StringComparer.Ordinal)]))
            .Where(u => u.ReportTypes.Count > 0)
            .OrderBy(u => scope.ObservedUnitIds.Contains(u.UnitId))
            .ThenBy(u => u.UnitName, StringComparer.Ordinal)
            .ToList();

        return Result.Success<IReadOnlyList<ComparisonUnitOption>>(options);
    }
}
