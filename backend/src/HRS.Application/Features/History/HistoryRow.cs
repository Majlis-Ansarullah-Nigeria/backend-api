using HRS.Application.Features.Submissions.MyReports;

namespace HRS.Application.Features.History;

/// <summary>One past report (FR-HIS-001). SubmittedByLabel is "You" (the viewer submitted it),
/// "Previous head" (submitted by someone who no longer heads that unit), or the submitter's
/// actual name (the unit's current head, as seen by an observer, or a delegate).</summary>
public sealed record HistoryRow(
    Guid InstanceId,
    Guid WindowId,
    Guid ReportTypeId,
    string ReportTypeName,
    Guid UnitId,
    string UnitName,
    string PeriodLabel,
    DateTimeOffset? SubmittedAt,
    ReportLineStatus Status,
    string SubmittedByLabel,
    bool IsObserved);
