namespace HRS.Domain.Templates;

/// <summary>How often this report type's windows recur (FR-TPL-001); drives the schedule rules
/// that generate windows ahead of time (FR-WIN-002, M4.2). Distinct from <see cref="ReportTypeCategory"/>,
/// which is a display/classification label — a Survey or Event is usually AdHoc frequency.</summary>
public enum ReportTypeFrequency
{
    Monthly,
    Quarterly,
    Annual,
    AdHoc,
}
