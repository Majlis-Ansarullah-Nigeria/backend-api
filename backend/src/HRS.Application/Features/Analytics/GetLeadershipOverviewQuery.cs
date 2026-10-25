using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Admin;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Analytics;

public enum LeadershipPeriod
{
    Current,
    Previous,
    Year,
}

/// <summary>A headline figure at National level. Compare is the figure it's measured against:
/// the period before for Current/Previous, the first period of the range for Year.</summary>
public sealed record LeadershipFigure(string FieldKey, string Label, TemplateFieldType Type, string? UnitLabel, double? Value, double? Compare);

public sealed record LeadershipOverview(
    string? ReportTypeName,
    Guid? CurrentWindowId,
    string? CurrentPeriodLabel,
    string? PreviousPeriodLabel,
    LeadershipPeriod Period,
    string? PeriodLabel,
    int Received,
    int Expected,
    DateTimeOffset? ClosesAt,
    IReadOnlyList<LeadershipFigure> Figures,
    double? OnTimePercent,
    string? OnTimePeriodLabel,
    string? ZoneLevelName,
    IReadOnlyList<OverviewGroupProgress> Zones);

/// <summary>
/// The "Leadership overview (mobile)" board: for the main report type (General Monthly first),
/// reports received for the current period, the period before, or the last 12 periods; the first
/// three headline figures at National level from acknowledged reports (FR-DSH-002); the on-time
/// rate of the last closed period; and how each zone's lowest level is doing. Leadership and the
/// Secretariat only.
/// </summary>
public sealed record GetLeadershipOverviewQuery(Guid UserId, LeadershipPeriod Period) : IRequest<Result<LeadershipOverview>>, IRequiresFeature
{
    public FeatureModule Feature => FeatureModule.Dashboards;
}

public sealed class GetLeadershipOverviewQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GetLeadershipOverviewQuery, Result<LeadershipOverview>>
{
    private const int HeadlineFigures = 3;
    private const int YearPeriods = 12;

    public async ValueTask<Result<LeadershipOverview>> Handle(GetLeadershipOverviewQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Leadership, cancellationToken)
            && !await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure<LeadershipOverview>(Error.Forbidden("FORBIDDEN", "Only Leadership and the Secretariat have this overview."));
        }

        var now = clock.UtcNow;
        var reportType = (await db.ReportTypes.AsNoTracking().Where(r => r.ArchivedAt == null).ToListAsync(cancellationToken))
            .OrderBy(r => r.Category == ReportTypeCategory.GeneralMonthly ? 0 : 1)
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .FirstOrDefault();
        var empty = new LeadershipOverview(null, null, null, null, request.Period, null, 0, 0, null, [], null, null, null, []);
        if (reportType is null)
        {
            return Result.Success(empty);
        }

        var windows = (await db.SubmissionWindows.AsNoTracking()
                .Where(w => w.ReportTypeId == reportType.Id && w.OpensAt <= now)
                .ToListAsync(cancellationToken))
            .OrderByDescending(w => w.OpensAt)
            .Take(YearPeriods)
            .ToList();
        if (windows.Count == 0)
        {
            return Result.Success(empty with { ReportTypeName = reportType.Name });
        }

        var chosen = request.Period switch
        {
            LeadershipPeriod.Previous => windows.Skip(1).Take(1).ToList(),
            LeadershipPeriod.Year => windows,
            _ => windows.Take(1).ToList(),
        };

        var context = await ComplianceContext.LoadAsync(db, [.. windows.Select(w => w.Id)], cancellationToken);
        var received = 0;
        var expected = 0;
        foreach (var window in chosen)
        {
            var outcomes = context.ExpectedUnits(window).Select(u => context.Outcome(window, u, now)).ToList();
            received += outcomes.Count(o => ComplianceBuckets.IsSubmitted(o.Bucket));
            expected += outcomes.Count;
        }

        var lastClosed = windows.FirstOrDefault(w => now >= w.ClosesAt);
        double? onTimePercent = null;
        if (lastClosed is not null)
        {
            var outcomes = context.ExpectedUnits(lastClosed).Select(u => context.Outcome(lastClosed, u, now)).ToList();
            onTimePercent = outcomes.Count == 0 ? null : Math.Round(100.0 * outcomes.Count(o => o.Bucket == ComplianceBucket.OnTime) / outcomes.Count);
        }

        var zoneWindow = chosen.Count > 0 ? chosen[0] : windows[0];
        var zoneLevel = ComplianceContext.DisplayOrder
            .FirstOrDefault(l => l != UnitLevelCode.Department && zoneWindow.DueAtFor(l) is not null && context.Levels.ContainsKey(l));
        var zones = zoneWindow.DueAtFor(zoneLevel) is null
            ? []
            : ComplianceGroups.Build(context, zoneWindow, zoneLevel, now)
                .Select(g => new OverviewGroupProgress(g.GroupUnitId, g.Name, g.Expected, g.Submitted))
                .OrderByDescending(g => g.Expected == 0 ? 0 : (double)g.Submitted / g.Expected)
                .ToList();

        var figures = await HeadlineFiguresAsync(reportType, windows, chosen, request.Period, cancellationToken);

        return Result.Success(new LeadershipOverview(
            reportType.Name,
            windows[0].Id,
            windows[0].PeriodLabel,
            windows.Count > 1 ? windows[1].PeriodLabel : null,
            request.Period,
            request.Period == LeadershipPeriod.Year ? null : chosen.FirstOrDefault()?.PeriodLabel,
            received,
            expected,
            request.Period == LeadershipPeriod.Year ? null : chosen.FirstOrDefault()?.ClosesAt,
            figures,
            onTimePercent,
            lastClosed?.PeriodLabel,
            zones.Count > 0 ? context.Levels[zoneLevel].Name : null,
            zones));
    }

    private async Task<IReadOnlyList<LeadershipFigure>> HeadlineFiguresAsync(
        ReportType reportType,
        List<Domain.Windows.SubmissionWindow> windows,
        List<Domain.Windows.SubmissionWindow> chosen,
        LeadershipPeriod period,
        CancellationToken cancellationToken)
    {
        if (chosen.Count == 0)
        {
            return [];
        }

        var units = await DashboardScope.LoadUnitsAsync(db, cancellationToken);
        var national = units.FirstOrDefault(u => u.Level == UnitLevelCode.National && u.ParentId is null);
        var fields = (await IndicatorFields.LoadAsync(db, reportType.Id, cancellationToken)).Take(HeadlineFigures).ToList();
        if (national is null || fields.Count == 0)
        {
            return [];
        }

        var windowIds = windows.Select(w => w.Id).ToList();
        var fieldKeys = fields.Select(f => f.FieldKey).ToList();
        var metrics = await db.ReportMetrics.AsNoTracking()
            .Where(m => windowIds.Contains(m.WindowId) && fieldKeys.Contains(m.FieldKey))
            .ToListAsync(cancellationToken);
        var targets = await db.ReportFieldTargets.AsNoTracking().Where(t => t.ReportTypeId == reportType.Id).ToListAsync(cancellationToken);
        var calculator = new IndicatorCalculator(reportType.ApplicableLevels, units, metrics, targets);

        double? ValueAt(IndicatorField field, Domain.Windows.SubmissionWindow? window) =>
            window is null ? null : calculator.Value(national, field.FieldKey, field.Aggregation, window.Id);

        return [.. fields.Select(field =>
        {
            if (period == LeadershipPeriod.Year)
            {
                // Oldest first, so "compare" is the start of the year and "value" the latest figure.
                var values = Enumerable.Reverse(chosen).Select(w => ValueAt(field, w)).Where(v => v is not null).ToList();
                return new LeadershipFigure(field.FieldKey, field.Label, field.Type, field.UnitLabel, values.LastOrDefault(), values.Count > 1 ? values[0] : null);
            }

            var index = windows.IndexOf(chosen[0]);
            var before = index + 1 < windows.Count ? windows[index + 1] : null;
            return new LeadershipFigure(field.FieldKey, field.Label, field.Type, field.UnitLabel, ValueAt(field, chosen[0]), ValueAt(field, before));
        })];
    }
}
