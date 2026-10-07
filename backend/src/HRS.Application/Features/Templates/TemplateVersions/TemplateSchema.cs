using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Application.Features.Templates.TemplateVersions;

/// <summary>
/// The structured shape stored (serialized) as TemplateVersion.SchemaJson. This is the data
/// model only — conditional-logic evaluation (M3.4), computed-expression evaluation (M3.3) and
/// the drag-and-drop builder UI (M3.5) all read and write this same shape, but none of that
/// evaluation logic lives here.
/// </summary>
public sealed record TemplateSchema(IReadOnlyList<TemplateSection> Sections);

public sealed record TemplateSection(string Key, string Title, IReadOnlyList<TemplateField> Fields);

public sealed record TemplateField(
    string Key,
    TemplateFieldType Type,
    string Label,
    string? HelpText,
    bool Required,
    double? Min,
    double? Max,
    string? Regex,
    string? DefaultValue,
    string? UnitLabel,
    bool Confidential,
    Guid? OwningDepartmentId,
    IReadOnlyList<UnitLevelCode>? VisibleForLevels,
    TemplateShowIf? ShowIf,
    AggregationRule Aggregation,
    string? ComputedExpression,
    IReadOnlyList<TemplateFieldOption>? Options,
    IReadOnlyList<TemplateField>? Columns,
    string? Placeholder = null);

public sealed record TemplateFieldOption(string Value, string Label);

/// <summary>A simple equality trigger (FR-TPL-004's own example: "If 'Held general meeting?' =
/// Yes, ask attendance"). Structural only here — evaluating it against real answers is M3.4.</summary>
public sealed record TemplateShowIf(string FieldKey, string EqualsValue);
