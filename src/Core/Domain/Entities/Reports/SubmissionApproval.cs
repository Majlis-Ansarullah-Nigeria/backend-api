using ManagementApi.Domain.Common;
using ManagementApi.Domain.Enums;

namespace ManagementApi.Domain.Entities.Reports;

/// <summary>
/// Represents an approval or rejection of a report submission
/// </summary>
public class SubmissionApproval : AuditableEntity
{
    public Guid ReportSubmissionId { get; private set; }
    public Guid ApproverId { get; private set; }
    public string ApproverName { get; private set; } = default!;
    public ApprovalStatus Status { get; private set; }
    public string? Comments { get; private set; }
    public DateTime ActionDate { get; private set; }

    // Navigation properties
    public ReportSubmission ReportSubmission { get; private set; } = default!;

    private SubmissionApproval() { } // EF Core

    public SubmissionApproval(
        Guid reportSubmissionId,
        Guid approverId,
        string approverName,
        ApprovalStatus status,
        string? comments = null)
    {
        ReportSubmissionId = reportSubmissionId;
        ApproverId = approverId;
        ApproverName = approverName;
        Status = status;
        Comments = comments;
        ActionDate = DateTime.UtcNow;
    }

    public void UpdateComments(string comments)
    {
        Comments = comments;
    }
}
