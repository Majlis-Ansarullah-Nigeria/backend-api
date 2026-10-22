using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Analytics;

public enum MapMeasure
{
    Indicator,
    Compliance,
}

/// <summary>A unit on the map. Value is the selected figure (Indicator) or the percentage of the
/// unit's expected reports submitted, counting its whole subtree (Compliance).</summary>
public sealed record MapUnit(Guid UnitId, string Name, string Code, UnitLevelCode Level, double? Latitude, double? Longitude, double? Value, bool HasChildren);

public sealed record MapView(
    MapMeasure Measure,
    IndicatorField? Field,
    string? PeriodLabel,
    IReadOnlyList<MapUnit> Tiles,
    IReadOnlyList<MapUnit> Pins);

/// <summary>
/// FR-DSH-004's map view for one dashboard node: its children as schematic tiles, and every unit
/// in its subtree that has a GPS location as a pin — each coloured by an indicator value (from the
/// latest period that has one) or by compliance in the latest window. Same scope rules as the
/// indicator dashboard (FR-DSH-005).
/// </summary>
public sealed record GetMapViewQuery(Guid UserId, Guid? ReportTypeId, string? FieldKey, Guid? UnitId, MapMeasure Measure)
    : IRequest<Result<MapView>>;

public sealed class GetMapViewQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GetMapViewQuery, Result<MapView>>
{
    private const int LookbackPeriods = 12;

    public async ValueTask<Result<MapView>> Handle(GetMapViewQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var units = await DashboardScope.LoadUnitsAsync(db, cancellationToken);
        var scope = await DashboardScope.ResolveAsync(db, accessPolicy, request.UserId, units, now, cancellationToken);
        if (!scope.HasAccess)
        {
            return Result.Failure<MapView>(Error.Forbidden("FORBIDDEN", "You don't have a dashboard."));
        }

        var resolved = DashboardScope.ResolveNode(scope, units, request.UnitId);
        if (resolved.IsFailure)
        {
            return Result.Failure<MapView>(resolved.Error);
        }

        var node = resolved.Value.Node;
        var reportTypes = await db.ReportTypes.AsNoTracking().Where(r => r.ArchivedAt == null).ToListAsync(cancellationToken);
        var reportType = request.ReportTypeId is { } requestedId
            ? reportTypes.FirstOrDefault(r => r.Id == requestedId)
            : reportTypes.OrderBy(r => r.Category == ReportTypeCategory.GeneralMonthly ? 0 : 1).ThenBy(r => r.Name, StringComparer.Ordinal).FirstOrDefault();
        if (reportType is null)
        {
            return request.ReportTypeId is null
                ? Result.Success(new MapView(request.Measure, null, null, [], []))
                : Result.Failure<MapView>(Error.NotFound("REPORT_TYPE_NOT_FOUND", "That report type doesn't exist."));
        }

        var windows = (await db.SubmissionWindows.AsNoTracking()
                .Where(w => w.ReportTypeId == reportType.Id && w.OpensAt <= now)
                .ToListAsync(cancellationToken))
            .OrderByDescending(w => w.OpensAt)
            .Take(LookbackPeriods)
            .ToList();

        var subtree = units.Where(u => u.Path == node.Path || u.Path.StartsWith(node.Path + ".", StringComparison.Ordinal)).ToList();
        var subtreeIds = subtree.Select(u => u.Id).ToList();
        var details = await db.Units.AsNoTracking()
            .Where(u => subtreeIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Code, u.Latitude, u.Longitude })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var targets = await db.ReportFieldTargets.AsNoTracking().Where(t => t.ReportTypeId == reportType.Id).ToListAsync(cancellationToken);
        IndicatorField? field = null;
        string? periodLabel = null;
        Func<IndicatorCalculator.UnitNode, double?> valueOf;
        IndicatorCalculator calculator;

        if (request.Measure == MapMeasure.Indicator)
        {
            var fields = await IndicatorFields.LoadAsync(db, reportType.Id, cancellationToken);
            field = request.FieldKey is { } key ? fields.FirstOrDefault(f => f.FieldKey == key) : (fields.Count > 0 ? fields[0] : null);
            if (request.FieldKey is not null && field is null)
            {
                return Result.Failure<MapView>(Error.NotFound("FIELD_NOT_FOUND", "That report type has no such figure."));
            }

            var windowIds = windows.Select(w => w.Id).ToList();
            var fieldKey = field?.FieldKey;
            var metrics = fieldKey is null
                ? []
                : await db.ReportMetrics.AsNoTracking()
                    .Where(m => windowIds.Contains(m.WindowId) && m.FieldKey == fieldKey
                        && (m.UnitPath == node.Path || m.UnitPath.StartsWith(node.Path + ".")))
                    .ToListAsync(cancellationToken);
            calculator = new IndicatorCalculator(reportType.ApplicableLevels, units, metrics, targets);

            // The latest period the node itself has a figure for, so every unit is coloured from
            // the same period.
            var period = field is null ? null : windows.FirstOrDefault(w => calculator.Value(node, field.FieldKey, field.Aggregation, w.Id) is not null);
            periodLabel = period?.PeriodLabel;
            valueOf = unit => field is null || period is null ? null : calculator.Value(unit, field.FieldKey, field.Aggregation, period.Id);
        }
        else
        {
            calculator = new IndicatorCalculator(reportType.ApplicableLevels, units, [], targets);
            var latest = windows.FirstOrDefault();
            periodLabel = latest?.PeriodLabel;
            var context = await ComplianceContext.LoadAsync(db, latest is null ? [] : [latest.Id], cancellationToken);
            var outcomes = latest is null
                ? []
                : context.ExpectedUnits(latest).Select(u => context.Outcome(latest, u, now)).ToList();
            valueOf = unit =>
            {
                var inSubtree = outcomes.Where(o => o.Unit.Path == unit.Path || o.Unit.Path.StartsWith(unit.Path + ".", StringComparison.Ordinal)).ToList();
                return inSubtree.Count == 0 ? null : Math.Round(100.0 * inSubtree.Count(o => ComplianceBuckets.IsSubmitted(o.Bucket)) / inSubtree.Count);
            };
        }

        MapUnit ToMapUnit(IndicatorCalculator.UnitNode unit)
        {
            var detail = details.GetValueOrDefault(unit.Id);
            return new MapUnit(unit.Id, unit.Name, detail?.Code ?? string.Empty, unit.Level, detail?.Latitude, detail?.Longitude,
                valueOf(unit), calculator.Children(unit).Count > 0);
        }

        var tiles = calculator.Children(node).Select(ToMapUnit).ToList();
        var pins = subtree
            .Where(u => calculator.IsRelevant(u) && details.GetValueOrDefault(u.Id) is { Latitude: not null, Longitude: not null })
            .Select(ToMapUnit)
            .ToList();

        return Result.Success(new MapView(request.Measure, field, periodLabel, tiles, pins));
    }
}
