using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Admin;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Analytics;

public sealed record DashboardReportTypeOption(Guid Id, string Name);

/// <summary>A level tab. Names come from UnitLevel — configurable data, never hard-coded.</summary>
public sealed record DashboardLevelOption(UnitLevelCode Code, string Name, string PluralName);

/// <summary>One period's bar: how the expected units at the chosen level ended up. IsClosed is
/// false for a window still open (the mockup's paler "open" bar).</summary>
public sealed record CompliancePeriodSummary(
    Guid WindowId,
    string PeriodLabel,
    DateTimeOffset? DueAt,
    bool IsClosed,
    int Expected,
    int OnTime,
    int Late,
    int Missed,
    int Drafting,
    int NotStarted);

/// <summary>One row of the "{period} so far, by zone" table. Remindable is how many units a
/// Remind click would reach right now.</summary>
public sealed record ComplianceGroupRow(
    Guid? GroupUnitId,
    string Name,
    int Expected,
    int Submitted,
    int Drafting,
    int NotStarted,
    int Remindable);

public sealed record ComplianceDashboard(
    IReadOnlyList<DashboardReportTypeOption> ReportTypes,
    Guid? ReportTypeId,
    string? ReportTypeName,
    IReadOnlyList<DashboardLevelOption> Levels,
    UnitLevelCode? Level,
    IReadOnlyList<CompliancePeriodSummary> Periods,
    IReadOnlyList<ComplianceGroupRow> Groups,
    bool CanRemind);

/// <summary>
/// FR-DSH-001's compliance dashboard: for one report type and one level, the last 12 periods'
/// on-time/late/missed split and, for the latest period, a per-zone table with one-click
/// reminders. National-wide, so only the Secretariat and Leadership see it (SRS §3.2 "National
/// dashboards"); Leadership is read-only — CanRemind is false and the reminder command refuses
/// them. Zone/Dila-scoped views are FR-DSH-005 (M10.2).
/// </summary>
public sealed record GetComplianceDashboardQuery(Guid UserId, Guid? ReportTypeId, UnitLevelCode? Level)
    : IRequest<Result<ComplianceDashboard>>, IRequiresFeature
{
    public FeatureModule Feature => FeatureModule.Dashboards;
}

public sealed class GetComplianceDashboardQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GetComplianceDashboardQuery, Result<ComplianceDashboard>>
{
    private const int PeriodCount = 12;

    public async ValueTask<Result<ComplianceDashboard>> Handle(GetComplianceDashboardQuery request, CancellationToken cancellationToken)
    {
        var isSecretariat = await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken);
        if (!isSecretariat && !await accessPolicy.HasRoleAsync(request.UserId, RoleName.Leadership, cancellationToken))
        {
            return Result.Failure<ComplianceDashboard>(Error.Forbidden("FORBIDDEN", "Only the Secretariat and Leadership see national dashboards."));
        }

        var reportTypes = await db.ReportTypes.AsNoTracking().Where(r => r.ArchivedAt == null).ToListAsync(cancellationToken);
        var options = reportTypes
            .OrderBy(r => r.Category == ReportTypeCategory.GeneralMonthly ? 0 : 1)
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .Select(r => new DashboardReportTypeOption(r.Id, r.Name))
            .ToList();

        var reportType = request.ReportTypeId is { } requestedId
            ? reportTypes.FirstOrDefault(r => r.Id == requestedId)
            : reportTypes.FirstOrDefault(r => r.Id == options.FirstOrDefault()?.Id);
        if (request.ReportTypeId is not null && reportType is null)
        {
            return Result.Failure<ComplianceDashboard>(Error.NotFound("REPORT_TYPE_NOT_FOUND", "That report type doesn't exist."));
        }

        var unitLevels = await db.UnitLevels.AsNoTracking().ToDictionaryAsync(l => l.Code, cancellationToken);
        if (reportType is null)
        {
            return Result.Success(new ComplianceDashboard(options, null, null, [], null, [], [], false));
        }

        var levels = ComplianceContext.DisplayOrder
            .Where(reportType.ApplicableLevels.Contains)
            .Select(code => new DashboardLevelOption(code, unitLevels[code].Name, unitLevels[code].PluralName))
            .ToList();
        var level = request.Level ?? levels.FirstOrDefault()?.Code;
        if (level is not { } chosenLevel || !reportType.ApplicableLevels.Contains(chosenLevel))
        {
            return Result.Failure<ComplianceDashboard>(Error.Validation("LEVEL_NOT_APPLICABLE", "This report type isn't filed at that level."));
        }

        var now = clock.UtcNow;
        var windows = (await db.SubmissionWindows.AsNoTracking()
                .Where(w => w.ReportTypeId == reportType.Id && w.OpensAt <= now)
                .ToListAsync(cancellationToken))
            .Where(w => w.DueAtFor(chosenLevel) is not null)
            .OrderByDescending(w => w.OpensAt)
            .Take(PeriodCount)
            .Reverse()
            .ToList();

        var context = await ComplianceContext.LoadAsync(db, [.. windows.Select(w => w.Id)], cancellationToken);

        var periods = windows
            .Select(window =>
            {
                var outcomes = context.ExpectedUnits(window, chosenLevel).Select(u => context.Outcome(window, u, now)).ToList();
                int Count(ComplianceBucket bucket) => outcomes.Count(o => o.Bucket == bucket);
                return new CompliancePeriodSummary(
                    window.Id,
                    window.PeriodLabel,
                    window.DueAtFor(chosenLevel),
                    now >= window.ClosesAt,
                    outcomes.Count,
                    Count(ComplianceBucket.OnTime),
                    Count(ComplianceBucket.Late),
                    Count(ComplianceBucket.Missed),
                    Count(ComplianceBucket.Drafting),
                    Count(ComplianceBucket.NotStarted) + Count(ComplianceBucket.Upcoming));
            })
            .ToList();

        var latest = windows.LastOrDefault();
        var groups = latest is null
            ? []
            : ComplianceGroups.Build(context, latest, chosenLevel, now);

        return Result.Success(new ComplianceDashboard(
            options, reportType.Id, reportType.Name, levels, chosenLevel, periods, groups,
            CanRemind: isSecretariat && latest is not null && now < latest.ClosesAt));
    }
}

public static class ComplianceGroups
{
    /// <summary>The per-zone (or per-department) table for one window and level. Drafting means
    /// a saved draft exists — whether the window is still open or has closed on it.</summary>
    public static IReadOnlyList<ComplianceGroupRow> Build(
        ComplianceContext context, Domain.Windows.SubmissionWindow window, UnitLevelCode level, DateTimeOffset now) =>
        context.ExpectedUnits(window, level)
            .Select(u => context.Outcome(window, u, now))
            .GroupBy(o => context.GroupOf(o.Unit))
            .Select(g => new ComplianceGroupRow(
                g.Key.GroupUnitId,
                g.Key.Name,
                g.Count(),
                g.Count(o => ComplianceBuckets.IsSubmitted(o.Bucket)),
                g.Count(o => !ComplianceBuckets.IsSubmitted(o.Bucket) && o.Instance?.Status == Domain.Submissions.ReportInstanceStatus.Draft),
                g.Count(o => !ComplianceBuckets.IsSubmitted(o.Bucket) && o.Instance?.Status != Domain.Submissions.ReportInstanceStatus.Draft),
                g.Count(o => ComplianceBuckets.IsRemindable(o.Bucket))))
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .ToList();
}
