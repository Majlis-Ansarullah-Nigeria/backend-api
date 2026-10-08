namespace HRS.Application.Features.Submissions.MyReports;

public sealed record MyReportSummary(
    Guid? InstanceId,
    Guid WindowId,
    Guid ReportTypeId,
    string ReportTypeName,
    Guid UnitId,
    string UnitName,
    string PeriodLabel,
    DateTimeOffset DueAt,
    DateTimeOffset? SubmittedAt,
    ReportLineStatus Status);
