using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Analytics;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Domain.Windows;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Review.Compiled;

/// <summary>
/// Builds FR-REV-006's compiled national report for one window from what's acknowledged right
/// now — so it refreshes as reports are acknowledged. National figures and zone figures come from
/// IndicatorCalculator (each unit's own acknowledged report, combined upward by the field's
/// aggregation rule); the submission overview from ComplianceContext. Highlights are facts read
/// off the zone figures, never generated prose; the narrative is the Secretariat's own text.
/// </summary>
public static class CompiledReportBuilder
{
    public static async Task<CompiledReportView> BuildAsync(
        IAppDbContext db, SubmissionWindow window, bool canEdit, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var reportType = await db.ReportTypes.AsNoTracking().FirstAsync(r => r.Id == window.ReportTypeId, cancellationToken);
        var previous = (await db.SubmissionWindows.AsNoTracking()
                .Where(w => w.ReportTypeId == window.ReportTypeId && w.OpensAt < window.OpensAt)
                .Select(w => new { w.Id, w.PeriodLabel, w.OpensAt })
                .ToListAsync(cancellationToken))
            .OrderByDescending(w => w.OpensAt)
            .FirstOrDefault();

        var context = await ComplianceContext.LoadAsync(db, [window.Id], cancellationToken);
        var outcomes = context.ExpectedUnits(window).Select(u => context.Outcome(window, u, now)).ToList();
        var levels = ComplianceContext.DisplayOrder
            .Where(level => context.Levels.ContainsKey(level) && outcomes.Any(o => o.Unit.Level == level))
            .Select(level =>
            {
                var atLevel = outcomes.Where(o => o.Unit.Level == level).ToList();
                return new CompiledLevelCount(
                    level, context.Levels[level].Name, context.Levels[level].PluralName,
                    atLevel.Count(o => ComplianceBuckets.IsSubmitted(o.Bucket)), atLevel.Count);
            })
            .ToList();
        var overview = new CompiledOverview(
            outcomes.Count,
            outcomes.Count(o => o.Bucket == ComplianceBucket.OnTime),
            outcomes.Count(o => o.Bucket == ComplianceBucket.Late),
            outcomes.Count(o => !ComplianceBuckets.IsSubmitted(o.Bucket)),
            outcomes.Count(o => o.Instance?.Status == ReportInstanceStatus.Acknowledged),
            levels);

        var sections = await IndicatorFields.LoadSectionsAsync(db, reportType.Id, cancellationToken);
        var fields = sections.SelectMany(s => s.Fields).ToList();
        var windowIds = previous is null ? new List<Guid> { window.Id } : [window.Id, previous.Id];
        var metrics = await db.ReportMetrics.AsNoTracking().Where(m => windowIds.Contains(m.WindowId)).ToListAsync(cancellationToken);
        var targets = await db.ReportFieldTargets.AsNoTracking().Where(t => t.ReportTypeId == reportType.Id).ToListAsync(cancellationToken);
        var units = await DashboardScope.LoadUnitsAsync(db, cancellationToken);
        var calculator = new IndicatorCalculator(reportType.ApplicableLevels, units, metrics, targets);
        var national = units.FirstOrDefault(u => u.Level == UnitLevelCode.National && u.ParentId is null);
        var zones = national is null ? [] : calculator.Children(national).Where(u => u.Level == UnitLevelCode.Zone).ToList();

        double? ValueAt(IndicatorCalculator.UnitNode unit, IndicatorField field, Guid? windowId) =>
            windowId is { } id ? calculator.Value(unit, field.FieldKey, field.Aggregation, id) : null;

        var compiledSections = sections
            .Select(section => new CompiledSection(
                section.Key,
                section.Title,
                [.. section.Fields.Select(field => new CompiledFigure(
                    field.FieldKey, field.Label, field.Type, field.UnitLabel,
                    national is null ? null : ValueAt(national, field, window.Id),
                    national is null ? null : ValueAt(national, field, previous?.Id),
                    national is null ? null : calculator.Target(national, field.FieldKey, field.Aggregation)))],
                [.. section.Fields.SelectMany(field => Highlights(field, zones, zone => ValueAt(zone, field, window.Id), zone => ValueAt(zone, field, previous?.Id)))]))
            .ToList();

        var compiledZones = zones
            .Select(zone =>
            {
                var inZone = outcomes.Where(o => context.GroupOf(o.Unit).GroupUnitId == zone.Id).ToList();
                return new CompiledZone(
                    zone.Id, zone.Name,
                    inZone.Count(o => ComplianceBuckets.IsSubmitted(o.Bucket)), inZone.Count,
                    [.. fields.Select(field => ValueAt(zone, field, window.Id))]);
            })
            .ToList();

        var narrative = await db.CompiledReports.AsNoTracking().FirstOrDefaultAsync(c => c.WindowId == window.Id, cancellationToken);

        return new CompiledReportView(
            window.Id,
            reportType.Id,
            reportType.Name,
            window.PeriodLabel,
            previous?.PeriodLabel,
            ReportReferenceNumber.GenerateNational(reportType.Category, window.PeriodLabel, window.OpensAt),
            now,
            canEdit,
            narrative?.Narrative ?? string.Empty,
            narrative?.NarrativeUpdatedAt,
            overview,
            compiledSections,
            fields,
            compiledZones);
    }

    /// <summary>Per figure: the zone with the highest value, and the largest rise and fall since the
    /// previous period (only when there was a rise or fall).</summary>
    public static IEnumerable<CompiledHighlight> Highlights(
        IndicatorField field,
        IReadOnlyList<IndicatorCalculator.UnitNode> zones,
        Func<IndicatorCalculator.UnitNode, double?> current,
        Func<IndicatorCalculator.UnitNode, double?> previous)
    {
        var rows = zones
            .Select(zone => (Zone: zone, Value: current(zone), Previous: previous(zone)))
            .Where(r => r.Value is not null)
            .Select(r => (r.Zone, Value: r.Value!.Value, Change: r.Previous is { } p ? r.Value!.Value - p : (double?)null))
            .ToList();
        if (rows.Count == 0)
        {
            yield break;
        }

        var highest = rows.MaxBy(r => r.Value);
        yield return new CompiledHighlight(CompiledHighlightKind.Highest, field.FieldKey, field.Label, highest.Zone.Name, highest.Value, highest.Change);

        var changed = rows.Where(r => r.Change is not null).ToList();
        if (changed.Count > 0 && changed.MaxBy(r => r.Change) is { Change: > 0 } rise)
        {
            yield return new CompiledHighlight(CompiledHighlightKind.LargestRise, field.FieldKey, field.Label, rise.Zone.Name, rise.Value, rise.Change);
        }

        if (changed.Count > 0 && changed.MinBy(r => r.Change) is { Change: < 0 } fall)
        {
            yield return new CompiledHighlight(CompiledHighlightKind.LargestFall, field.FieldKey, field.Label, fall.Zone.Name, fall.Value, fall.Change);
        }
    }
}
