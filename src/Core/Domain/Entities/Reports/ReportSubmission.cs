using ManagementApi.Domain.Common;
using ManagementApi.Domain.Enums;

namespace ManagementApi.Domain.Entities.Reports;

/// <summary>
/// Represents a user's submission of a report
/// </summary>
public class ReportSubmission : AuditableEntity, IAggregateRoot
{
    public Guid ReportTemplateId { get; private set; }
    public Guid SubmitterId { get; private set; } // User who submitted
    public Guid? SubmissionWindowId { get; private set; }
    public string SubmitterChandaNo { get; private set; } = default!;
    public string SubmitterName { get; private set; } = default!;
    public string? SubmitterEmail { get; private set; }

    // Organization context
    public Guid? MuqamId { get; private set; }
    public Guid? DilaId { get; private set; }
    public Guid? ZoneId { get; private set; }
    public OrganizationLevel OrganizationLevel { get; private set; }

    // Submission data
    public string ResponseData { get; private set; } = default!; // JSON object with question IDs as keys
    public SubmissionStatus Status { get; private set; } = SubmissionStatus.Draft;
    public DateTime? SubmittedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    private readonly List<SubmissionApproval> _approvals = new();
    public IReadOnlyCollection<SubmissionApproval> Approvals => _approvals.AsReadOnly();

    // Navigation properties
    public ReportTemplate ReportTemplate { get; private set; } = default!;
    public SubmissionWindow? SubmissionWindow { get; private set; }

    private ReportSubmission() { } // EF Core

    public ReportSubmission(
        Guid reportTemplateId,
        Guid submitterId,
        string submitterChandaNo,
        string submitterName,
        OrganizationLevel organizationLevel,
        string responseData,
        string? submitterEmail = null,
        Guid? muqamId = null,
        Guid? dilaId = null,
        Guid? zoneId = null,
        Guid? submissionWindowId = null)
    {
        ReportTemplateId = reportTemplateId;
        SubmitterId = submitterId;
        SubmitterChandaNo = submitterChandaNo;
        SubmitterName = submitterName;
        SubmitterEmail = submitterEmail;
        OrganizationLevel = organizationLevel;
        ResponseData = responseData;
        MuqamId = muqamId;
        DilaId = dilaId;
        ZoneId = zoneId;
        SubmissionWindowId = submissionWindowId;
        Status = SubmissionStatus.Draft;
    }

    public void UpdateResponseData(string responseData)
    {
        ResponseData = responseData;
    }

    public void Submit()
    {
        if (Status == SubmissionStatus.Draft)
        {
            Status = SubmissionStatus.Submitted;
            SubmittedAt = DateTime.UtcNow;
        }
    }

    public void Approve(Guid approverId, string approverName, string? comments = null)
    {
        if (Status != SubmissionStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted reports can be approved");
        }

        var approval = new SubmissionApproval(Id, approverId, approverName, ApprovalStatus.Approved, comments);
        _approvals.Add(approval);
        Status = SubmissionStatus.Approved;
    }

    public void Reject(Guid approverId, string approverName, string reason)
    {
        if (Status != SubmissionStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted reports can be rejected");
        }

        var approval = new SubmissionApproval(Id, approverId, approverName, ApprovalStatus.Rejected, reason);
        _approvals.Add(approval);
        Status = SubmissionStatus.Rejected;
        RejectionReason = reason;
    }

    public void ReturnToDraft()
    {
        if (Status == SubmissionStatus.Rejected)
        {
            Status = SubmissionStatus.Draft;
            SubmittedAt = null;
            RejectionReason = null;
        }
    }
}
