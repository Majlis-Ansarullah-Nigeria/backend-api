namespace HRS.Domain.Analytics;

/// <summary>
/// One numeric answer of one acknowledged report, copied out of the JSONB answers so indicator
/// dashboards (FR-DSH-002) aggregate over a narrow table instead of parsing every report (SRS §8
/// ReportMetric, architecture.md). Derived data: rewritten in full whenever its report is
/// acknowledged, never edited. UnitPath is copied for subtree (prefix) queries.
/// </summary>
public sealed class ReportMetric
{
    private ReportMetric()
    {
    }

    private ReportMetric(Guid id, Guid reportInstanceId, Guid windowId, Guid unitId, string unitPath, string fieldKey, decimal value, DateTimeOffset periodStart)
    {
        Id = id;
        ReportInstanceId = reportInstanceId;
        WindowId = windowId;
        UnitId = unitId;
        UnitPath = unitPath;
        FieldKey = fieldKey;
        Value = value;
        PeriodStart = periodStart;
    }

    public Guid Id { get; private set; }

    public Guid ReportInstanceId { get; private set; }

    public Guid WindowId { get; private set; }

    public Guid UnitId { get; private set; }

    public string UnitPath { get; private set; } = string.Empty;

    public string FieldKey { get; private set; } = string.Empty;

    public decimal Value { get; private set; }

    /// <summary>The window's OpensAt — orders periods without joining windows.</summary>
    public DateTimeOffset PeriodStart { get; private set; }

    public static ReportMetric Create(
        Guid id, Guid reportInstanceId, Guid windowId, Guid unitId, string unitPath, string fieldKey, decimal value, DateTimeOffset periodStart) =>
        new(id, reportInstanceId, windowId, unitId, unitPath, fieldKey, value, periodStart);
}
