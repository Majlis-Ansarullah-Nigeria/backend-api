using HRS.Application.Features.Analytics;
using HRS.Domain.Organization;

namespace HRS.Application.Features.Review.Compiled;

public sealed record CompiledLevelCount(UnitLevelCode Level, string LevelName, string LevelPluralName, int Submitted, int Expected);

/// <summary>"Of N units expected to report, X submitted on time, Y late and Z did not report."</summary>
public sealed record CompiledOverview(int Expected, int OnTime, int Late, int NotReported, int Acknowledged, IReadOnlyList<CompiledLevelCount> Levels);

/// <summary>One national figure: this period, the period before, and the national target.</summary>
public sealed record CompiledFigure(string FieldKey, string Label, Domain.Templates.TemplateFieldType Type, string? UnitLabel, double? Value, double? Previous, double? Target);

public enum CompiledHighlightKind
{
    Highest,
    LargestRise,
    LargestFall,
}

/// <summary>A factual highlight drawn from the zone figures — never generated prose.</summary>
public sealed record CompiledHighlight(CompiledHighlightKind Kind, string FieldKey, string FieldLabel, string ZoneName, double Value, double? Change);

public sealed record CompiledSection(string Key, string Title, IReadOnlyList<CompiledFigure> Figures, IReadOnlyList<CompiledHighlight> Highlights);

/// <summary>A zone's row in the appendix: its submissions and every figure (aligned with Fields).</summary>
public sealed record CompiledZone(Guid UnitId, string Name, int Submitted, int Expected, IReadOnlyList<double?> Values);

public sealed record CompiledReportView(
    Guid WindowId,
    Guid ReportTypeId,
    string ReportTypeName,
    string PeriodLabel,
    string? PreviousPeriodLabel,
    string ReferenceNumber,
    DateTimeOffset CompiledAt,
    bool CanEdit,
    string Narrative,
    DateTimeOffset? NarrativeUpdatedAt,
    CompiledOverview Overview,
    IReadOnlyList<CompiledSection> Sections,
    IReadOnlyList<IndicatorField> Fields,
    IReadOnlyList<CompiledZone> Zones);

public sealed record CompiledReportListItem(Guid WindowId, Guid ReportTypeId, string ReportTypeName, string PeriodLabel, DateTimeOffset OpensAt, bool IsClosed, bool HasNarrative);
