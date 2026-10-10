using HRS.Domain.Templates;

namespace HRS.Application.Features.Submissions.PullFigures;

/// <summary>One child unit as it appears in the "Included Muqamaat" panel (FR-SUB-007) — Status
/// mirrors ReportLineStatus's numeric values so the frontend reuses its existing StatusChip
/// mapping without a second status vocabulary.</summary>
public sealed record PullChildUnitOption(Guid UnitId, string UnitName, int Status);

public sealed record PullSummaryFieldBreakdownEntry(Guid UnitId, string UnitName, double Value);

public sealed record PullSummaryField(
    string FieldKey,
    AggregationRule Aggregation,
    double Value,
    bool IsOverridden,
    IReadOnlyList<PullSummaryFieldBreakdownEntry> Breakdown);

public sealed record PullSummary(
    DateTimeOffset PulledAt,
    IReadOnlyList<Guid> IncludedUnitIds,
    IReadOnlyList<Guid> MissingUnitIds,
    string? OverrideNote,
    IReadOnlyList<PullSummaryField> Fields);

/// <summary>Eligible is false (and everything else null/empty) when this report type doesn't
/// apply to both the caller's own level and their children's level, or the unit has no children —
/// a Muqam Head, for instance, never sees the "Pull from Muqamaat" button at all.</summary>
public sealed record PullContextView(
    bool Eligible,
    string? ChildLevelName,
    string? ChildLevelPluralName,
    IReadOnlyList<PullChildUnitOption> Children,
    PullSummary? Summary);
