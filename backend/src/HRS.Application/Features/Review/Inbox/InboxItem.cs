using HRS.Domain.Organization;
using HRS.Domain.Submissions;

namespace HRS.Application.Features.Review.Inbox;

/// <summary>FR-REV-001's inbox row. ZoneUnitId/ZoneName are null for a National-level or
/// Department report — neither has a Zone ancestor (UnitHierarchyRules: Department hangs off
/// National directly).</summary>
public sealed record InboxItem(
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
    bool IsLate,
    bool IsFlagged,
    Guid? AssignedToUserId,
    string? AssignedToName,
    DateTimeOffset? SubmittedAt,
    string? ReferenceNumber);
