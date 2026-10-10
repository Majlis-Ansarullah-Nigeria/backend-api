using HRS.Application.Features.Submissions.MyReports;

namespace HRS.Application.Features.Observer.ComplianceGrid;

/// <summary>A unit the caller heads and could pick as the grid's parent (FR-AC-002: a user may
/// hold several UnitHead assignments at once, each potentially with children of its own).</summary>
public sealed record ObservableUnitOption(Guid Id, string Name);

public sealed record ReportTypeOption(Guid Id, string Name);

/// <summary>One column. DueAt is the level's own due date for this window, not adjusted for any
/// unit-specific extension — a representative header date, not a per-row value.</summary>
public sealed record CompliancePeriod(Guid WindowId, string PeriodLabel, DateTimeOffset DueAt);

public sealed record ComplianceGridCell(Guid WindowId, ReportLineStatus Status, Guid? ReportInstanceId);

public sealed record ComplianceGridRow(
    Guid UnitId,
    string UnitName,
    string? HeadName,
    bool HasVacantHead,
    IReadOnlyList<ComplianceGridCell> Cells);

/// <summary>FR-OBS-001: Dila/Zone Heads' compliance grid of their children per report type and
/// period. ParentUnitId/Name and the child level's Name/PluralName are null only when the caller
/// has no observable unit at all (empty state, not an error).</summary>
public sealed record ComplianceGridResult(
    Guid? ParentUnitId,
    string? ParentUnitName,
    string? ChildLevelName,
    string? ChildLevelPluralName,
    IReadOnlyList<ObservableUnitOption> AvailableParentUnits,
    IReadOnlyList<ReportTypeOption> AvailableReportTypes,
    Guid? SelectedReportTypeId,
    IReadOnlyList<CompliancePeriod> Periods,
    IReadOnlyList<ComplianceGridRow> Rows);
