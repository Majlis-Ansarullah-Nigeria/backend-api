namespace HRS.Domain.Submissions;

/// <summary>The seven states from SRS §5.1. Acknowledged is the only final state; Returned
/// re-enters at Submitted via resubmission; Missed leaves only via a Secretariat reopen. On
/// time/late is a flag (IsLate), not a state — "Overdue" is a derived label for NotStarted/Draft
/// past its due date, never stored (see ReportInstance.IsOverdue).</summary>
public enum ReportInstanceStatus
{
    NotStarted,
    Draft,
    Submitted,
    InReview,
    Returned,
    Acknowledged,
    Missed,
}
