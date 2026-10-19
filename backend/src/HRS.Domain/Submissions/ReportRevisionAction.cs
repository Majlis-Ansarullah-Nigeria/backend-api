namespace HRS.Domain.Submissions;

/// <summary>FR-HIS-006's "every submission, withdrawal and revision". Resubmitted is any submit
/// after the first — following a Return (BR-WIN-06), a withdrawal (FR-SUB-009) or a Secretariat
/// reopen. Review actions (open, acknowledge, return) don't change answers and aren't recorded
/// here; return comments are review comments observers must never see (FR-AC-005).</summary>
public enum ReportRevisionAction
{
    Submitted,
    Withdrawn,
    Resubmitted,
}
