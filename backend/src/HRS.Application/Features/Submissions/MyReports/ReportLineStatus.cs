namespace HRS.Application.Features.Submissions.MyReports;

/// <summary>
/// A display status for one "my reports" row (FR-SUB-001) — derived, not stored. Combines
/// ReportInstance.Status (when an instance exists yet; most rows won't have one before M5.3's
/// first Save) with SubmissionWindow.StatusFor's window-level BeforeOpen/OnTime/Late/Closed, per
/// GetMyReportsQueryHandler.DetermineStatus. Mirrors frontend/src/components/shared/StatusChip.tsx's
/// ten variants exactly.
/// </summary>
public enum ReportLineStatus
{
    Upcoming,
    NotStarted,
    Draft,
    Overdue,
    Submitted,
    SubmittedLate,
    InReview,
    Returned,
    Acknowledged,
    Missed,
}
