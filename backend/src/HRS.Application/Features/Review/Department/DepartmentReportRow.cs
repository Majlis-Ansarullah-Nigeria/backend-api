using HRS.Domain.Organization;
using HRS.Domain.Submissions;

namespace HRS.Application.Features.Review.Department;

public sealed record DepartmentFieldValue(string FieldKey, string Label, string Value);

/// <summary>FR-REV-005's row: one submitted-or-later report, with only the fields this caller's
/// department owns (and that are visible at this unit's level) — never the whole report. A report
/// with no owned-and-visible fields never produces a row at all.</summary>
public sealed record DepartmentReportRow(
    Guid ReportId,
    Guid ReportTypeId,
    string ReportTypeName,
    Guid UnitId,
    string UnitName,
    UnitLevelCode UnitLevel,
    Guid? ZoneUnitId,
    string? ZoneName,
    Guid WindowId,
    string PeriodLabel,
    ReportInstanceStatus Status,
    DateTimeOffset? SubmittedAt,
    IReadOnlyList<DepartmentFieldValue> Fields);
