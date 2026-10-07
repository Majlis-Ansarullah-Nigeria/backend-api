namespace HRS.Domain.Templates;

/// <summary>Per-numeric-field roll-up rule (FR-TPL-006), driving Muqam→Dila→Zone→National
/// roll-ups and the "import child figures" feature (FR-SUB-007). None = 0 so a field that
/// never mentions aggregation defaults to not aggregating, not to an arbitrary rule.</summary>
public enum AggregationRule
{
    None,
    Sum,
    Average,
    Min,
    Max,
    Latest,
}
