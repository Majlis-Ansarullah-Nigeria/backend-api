using HRS.Domain.Analytics;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Application.Features.Analytics;

/// <summary>
/// FR-DSH-002/003's figures for any node of the tree. A unit at a level the report type is filed
/// at reports its own figure — the value in its own acknowledged report, never a re-sum of its
/// children (Dila and Zone reports already carry pulled-and-checked figures, FR-SUB-007). A node
/// the report type isn't filed at (National, or a level skipped by the type) combines its
/// children's figures with the field's aggregation rule. Targets (FR-TPL-011) follow the same
/// shape: a level's target for units that file, combined from the children otherwise — summed
/// for Sum fields, averaged for Average fields, none for the rest.
/// </summary>
public sealed class IndicatorCalculator
{
    public sealed record UnitNode(Guid Id, Guid? ParentId, string Name, UnitLevelCode Level, string Path);

    private readonly HashSet<UnitLevelCode> _filedLevels;
    private readonly Dictionary<Guid, List<UnitNode>> _childrenByParent;
    private readonly Dictionary<(Guid UnitId, string FieldKey, Guid WindowId), double> _metrics;
    private readonly Dictionary<(string FieldKey, UnitLevelCode Level), double> _targets;
    private readonly Dictionary<Guid, bool> _relevant = [];
    private readonly Dictionary<(Guid, string, AggregationRule, Guid), double?> _valueCache = [];

    public IndicatorCalculator(
        IEnumerable<UnitLevelCode> filedLevels,
        IReadOnlyCollection<UnitNode> units,
        IEnumerable<ReportMetric> metrics,
        IEnumerable<ReportFieldTarget> targets)
    {
        _filedLevels = [.. filedLevels];
        _childrenByParent = units
            .Where(u => u.ParentId is not null)
            .GroupBy(u => u.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(u => u.Name, StringComparer.Ordinal).ToList());
        _metrics = metrics.ToDictionary(m => (m.UnitId, m.FieldKey, m.WindowId), m => (double)m.Value);
        _targets = targets.ToDictionary(t => (t.FieldKey, t.Level), t => (double)t.Value);
    }

    public bool Files(UnitNode unit) => _filedLevels.Contains(unit.Level);

    /// <summary>Children whose subtree contains at least one unit that files this report type.</summary>
    public IReadOnlyList<UnitNode> Children(UnitNode unit) =>
        [.. _childrenByParent.GetValueOrDefault(unit.Id, []).Where(IsRelevant)];

    public bool IsRelevant(UnitNode unit)
    {
        if (_relevant.TryGetValue(unit.Id, out var cached))
        {
            return cached;
        }

        var relevant = Files(unit) || _childrenByParent.GetValueOrDefault(unit.Id, []).Any(IsRelevant);
        _relevant[unit.Id] = relevant;
        return relevant;
    }

    public double? Value(UnitNode unit, string fieldKey, AggregationRule aggregation, Guid windowId)
    {
        var cacheKey = (unit.Id, fieldKey, aggregation, windowId);
        if (_valueCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        double? value = Files(unit)
            ? _metrics.TryGetValue((unit.Id, fieldKey, windowId), out var own) ? own : null
            : Combine(aggregation, Children(unit).Select(child => Value(child, fieldKey, aggregation, windowId)));
        _valueCache[cacheKey] = value;
        return value;
    }

    public double? Target(UnitNode unit, string fieldKey, AggregationRule aggregation)
    {
        if (Files(unit))
        {
            return _targets.TryGetValue((fieldKey, unit.Level), out var target) ? target : null;
        }

        return aggregation is AggregationRule.Sum or AggregationRule.Average
            ? Combine(aggregation, Children(unit).Select(child => Target(child, fieldKey, aggregation)))
            : null;
    }

    /// <summary>Combines the children that have a figure; null when none do, or when the field
    /// has no meaningful cross-unit aggregation (None, Latest).</summary>
    public static double? Combine(AggregationRule aggregation, IEnumerable<double?> values)
    {
        var present = values.Where(v => v is not null).Select(v => v!.Value).ToList();
        if (present.Count == 0)
        {
            return null;
        }

        return aggregation switch
        {
            AggregationRule.Sum => present.Sum(),
            AggregationRule.Average => present.Average(),
            AggregationRule.Min => present.Min(),
            AggregationRule.Max => present.Max(),
            _ => null,
        };
    }
}
