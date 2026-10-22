using HRS.Domain.Organization;

namespace HRS.Domain.Templates;

/// <summary>
/// FR-TPL-011's field-level target for one numeric field of a report type, at one level — every
/// unit at that level shares it (for example new members: Muqam 20, Dila 120, Zone 700). Kept on
/// the report type rather than in a template version so the Secretariat can change a target
/// without publishing a new version; dashboards always use the current target. Keyed by the
/// field's stable key, so it carries across template versions.
/// </summary>
public sealed class ReportFieldTarget
{
    private ReportFieldTarget()
    {
    }

    private ReportFieldTarget(Guid id, Guid reportTypeId, string fieldKey, UnitLevelCode level, decimal value)
    {
        Id = id;
        ReportTypeId = reportTypeId;
        FieldKey = fieldKey;
        Level = level;
        SetValue(value);
    }

    public Guid Id { get; private set; }

    public Guid ReportTypeId { get; private set; }

    public string FieldKey { get; private set; } = string.Empty;

    public UnitLevelCode Level { get; private set; }

    public decimal Value { get; private set; }

    public static ReportFieldTarget Create(Guid id, Guid reportTypeId, string fieldKey, UnitLevelCode level, decimal value) =>
        new(id, reportTypeId, fieldKey, level, value);

    public void SetValue(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "A target can't be negative.");
        }

        Value = value;
    }
}
