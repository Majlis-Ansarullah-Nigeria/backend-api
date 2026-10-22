using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Analytics;

public sealed record IndicatorField(string FieldKey, string Label, TemplateFieldType Type, string? UnitLabel, AggregationRule Aggregation);

public sealed record IndicatorNode(Guid UnitId, string Name, UnitLevelCode Level, string LevelName);

public sealed record IndicatorPeriod(Guid WindowId, string PeriodLabel, DateTimeOffset OpensAt);

/// <summary>One field's figure for the node in every period (aligned with Periods), and its target.</summary>
public sealed record IndicatorSeries(string FieldKey, IReadOnlyList<double?> Values, double? Target);

/// <summary>A drill-down row for the selected field: the first and latest figures in the range,
/// and the child's target. HasChildren says whether the row drills further.</summary>
public sealed record IndicatorChildRow(Guid UnitId, string Name, UnitLevelCode Level, bool HasChildren, double? First, double? Latest, double? Target);

public sealed record IndicatorDashboard(
    IReadOnlyList<DashboardReportTypeOption> ReportTypes,
    Guid? ReportTypeId,
    string? ReportTypeName,
    IReadOnlyList<IndicatorField> Fields,
    string? FieldKey,
    IndicatorNode? Node,
    IReadOnlyList<IndicatorNode> Breadcrumb,
    string? ChildLevelPluralName,
    IReadOnlyList<IndicatorPeriod> Periods,
    IReadOnlyList<IndicatorSeries> Series,
    IReadOnlyList<IndicatorChildRow> Children);

/// <summary>
/// FR-DSH-002/003/005's indicator dashboard: every numeric field of a report type for one node of
/// the tree over the last N periods (acknowledged reports only, via report_metrics), its targets
/// (FR-TPL-011), and the node's children for drill-down on the selected field. Scope comes from
/// DashboardScope — a Zone or Dila head can only open nodes inside their own subtree.
/// </summary>
public sealed record GetIndicatorDashboardQuery(Guid UserId, Guid? ReportTypeId, string? FieldKey, Guid? UnitId, int? PeriodCount)
    : IRequest<Result<IndicatorDashboard>>;

public sealed class GetIndicatorDashboardQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GetIndicatorDashboardQuery, Result<IndicatorDashboard>>
{
    public const int DefaultPeriods = 12;

    public async ValueTask<Result<IndicatorDashboard>> Handle(GetIndicatorDashboardQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var units = await DashboardScope.LoadUnitsAsync(db, cancellationToken);
        var scope = await DashboardScope.ResolveAsync(db, accessPolicy, request.UserId, units, now, cancellationToken);
        if (!scope.HasAccess)
        {
            return Result.Failure<IndicatorDashboard>(Error.Forbidden("FORBIDDEN", "You don't have a dashboard."));
        }

        var node = request.UnitId is { } unitId ? units.FirstOrDefault(u => u.Id == unitId) : scope.Roots[0];
        if (node is null)
        {
            return Result.Failure<IndicatorDashboard>(Error.NotFound("UNIT_NOT_FOUND", "That unit doesn't exist."));
        }

        var root = scope.RootContaining(node);
        if (root is null)
        {
            return Result.Failure<IndicatorDashboard>(Error.Forbidden("FORBIDDEN", "That unit is outside your dashboard."));
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
            return Result.Failure<IndicatorDashboard>(Error.NotFound("REPORT_TYPE_NOT_FOUND", "That report type doesn't exist."));
        }

        var levelNames = await db.UnitLevels.AsNoTracking().ToDictionaryAsync(l => l.Code, cancellationToken);
        IndicatorNode ToNode(IndicatorCalculator.UnitNode unit) => new(unit.Id, unit.Name, unit.Level, levelNames[unit.Level].Name);
        var breadcrumb = units
            .Where(u => node.Path == u.Path || node.Path.StartsWith(u.Path + ".", StringComparison.Ordinal))
            .Where(u => u.Path == root.Path || u.Path.StartsWith(root.Path + ".", StringComparison.Ordinal))
            .OrderBy(u => u.Path.Length)
            .Select(ToNode)
            .ToList();

        if (reportType is null)
        {
            return Result.Success(new IndicatorDashboard(options, null, null, [], null, ToNode(node), breadcrumb, null, [], [], []));
        }

        var fields = await IndicatorFields.LoadAsync(db, reportType.Id, cancellationToken);
        var field = request.FieldKey is { } fieldKey ? fields.FirstOrDefault(f => f.FieldKey == fieldKey) : (fields.Count > 0 ? fields[0] : null);
        if (request.FieldKey is not null && field is null)
        {
            return Result.Failure<IndicatorDashboard>(Error.NotFound("FIELD_NOT_FOUND", "That report type has no such figure."));
        }

        var periodCount = Math.Clamp(request.PeriodCount ?? DefaultPeriods, 2, 36);
        var windows = (await db.SubmissionWindows.AsNoTracking()
                .Where(w => w.ReportTypeId == reportType.Id && w.OpensAt <= now)
                .Select(w => new { w.Id, w.PeriodLabel, w.OpensAt })
                .ToListAsync(cancellationToken))
            .OrderByDescending(w => w.OpensAt)
            .Take(periodCount)
            .OrderBy(w => w.OpensAt)
            .ToList();
        var windowIds = windows.Select(w => w.Id).ToList();

        var subtreePrefix = node.Path + ".";
        var metrics = await db.ReportMetrics.AsNoTracking()
            .Where(m => windowIds.Contains(m.WindowId) && (m.UnitPath == node.Path || m.UnitPath.StartsWith(subtreePrefix)))
            .ToListAsync(cancellationToken);
        var targets = await db.ReportFieldTargets.AsNoTracking().Where(t => t.ReportTypeId == reportType.Id).ToListAsync(cancellationToken);
        var calculator = new IndicatorCalculator(reportType.ApplicableLevels, units, metrics, targets);

        var series = fields
            .Select(f => new IndicatorSeries(
                f.FieldKey,
                [.. windows.Select(w => calculator.Value(node, f.FieldKey, f.Aggregation, w.Id))],
                calculator.Target(node, f.FieldKey, f.Aggregation)))
            .ToList();

        var children = calculator.Children(node);
        var childRows = field is null
            ? []
            : children.Select(child =>
                {
                    var values = windows.Select(w => calculator.Value(child, field.FieldKey, field.Aggregation, w.Id)).ToList();
                    return new IndicatorChildRow(
                        child.Id,
                        child.Name,
                        child.Level,
                        calculator.Children(child).Count > 0,
                        values.FirstOrDefault(v => v is not null),
                        values.LastOrDefault(v => v is not null),
                        calculator.Target(child, field.FieldKey, field.Aggregation));
                })
                .ToList();

        var childLevels = children.Select(c => c.Level).Distinct().ToList();
        var childLevelPluralName = childLevels.Count == 1 ? levelNames[childLevels[0]].PluralName : null;

        return Result.Success(new IndicatorDashboard(
            options,
            reportType.Id,
            reportType.Name,
            fields,
            field?.FieldKey,
            ToNode(node),
            breadcrumb,
            childLevelPluralName,
            [.. windows.Select(w => new IndicatorPeriod(w.Id, w.PeriodLabel, w.OpensAt))],
            series,
            childRows));
    }
}
