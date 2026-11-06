using ManagementApi.Domain.Common;
using ManagementApi.Domain.Enums;

namespace ManagementApi.Domain.Entities.Reports;

/// <summary>
/// A unit's report for one submission window. The report belongs to the unit
/// (OrganizationLevel + UnitId), not to the person who filled it: when a head changes,
/// the next head sees the unit's full history. Submitter fields record who acted.
/// </summary>
public class ReportSubmission : AuditableEntity, IAggregateRoot
{
    /// <summary>How long a returned report stays editable, even after the window closes.</summary>
    public static readonly TimeSpan RevisionPeriod = TimeSpan.FromDays(7);

    public Guid ReportTemplateId { get; private set; }
    public Guid SubmitterId { get; private set; } // User who last saved or submitted
    public Guid? SubmissionWindowId { get; private set; }
    public string SubmitterChandaNo { get; private set; } = default!;
    public string SubmitterName { get; private set; } = default!;
    public string? SubmitterEmail { get; private set; }

    // Owning unit. UnitId is the Muqam, Dila or Zone id for that level, Guid.Empty for National.
    public OrganizationLevel OrganizationLevel { get; private set; }
    public Guid UnitId { get; private set; }

    // Ancestors of the owning unit, for observer and dashboard queries
    public Guid? MuqamId { get; private set; }
    public Guid? DilaId { get; private set; }
    public Guid? ZoneId { get; private set; }

    // Submission data
    public string ResponseData { get; private set; } = default!; // JSON object with question IDs as keys
    public SubmissionStatus Status { get; private set; } = SubmissionStatus.Draft;
    public DateTime? SubmittedAt { get; private set; }
    public bool IsLate { get; private set; }

    /// <summary>Issued on first submission and kept for good, e.g. GMR-2026-11-Z03D12M04.</summary>
    public string? ReferenceNumber { get; private set; }

    // Review
    public DateTime? ReviewStartedAt { get; private set; }
    public DateTime? AcknowledgedAt { get; private set; }
    public DateTime? ReturnedAt { get; private set; }
    public string? ReturnReason { get; private set; }
    public DateTime? RevisionDueAt { get; private set; }

    private readonly List<SubmissionApproval> _approvals = new();
    public IReadOnlyCollection<SubmissionApproval> Approvals => _approvals.AsReadOnly();

    private readonly List<FileAttachment> _fileAttachments = new();
    public IReadOnlyCollection<FileAttachment> FileAttachments => _fileAttachments.AsReadOnly();

    // Navigation properties
    public ReportTemplate ReportTemplate { get; private set; } = default!;
    public SubmissionWindow? SubmissionWindow { get; private set; }

    private ReportSubmission() { } // EF Core

    public ReportSubmission(
        Guid reportTemplateId,
        Guid submissionWindowId,
        OrganizationLevel organizationLevel,
        Guid unitId,
        Guid? muqamId,
        Guid? dilaId,
        Guid? zoneId,
        Guid submitterId,
        string submitterChandaNo,
        string submitterName,
        string? submitterEmail,
        string responseData)
    {
        if (organizationLevel != OrganizationLevel.National && unitId == Guid.Empty)
        {
            throw new ArgumentException($"A {organizationLevel} report needs the {organizationLevel} it belongs to.", nameof(unitId));
        }

        ReportTemplateId = reportTemplateId;
        SubmissionWindowId = submissionWindowId;
        OrganizationLevel = organizationLevel;
        UnitId = organizationLevel == OrganizationLevel.National ? Guid.Empty : unitId;
        MuqamId = muqamId;
        DilaId = dilaId;
        ZoneId = zoneId;
        SetSubmitter(submitterId, submitterChandaNo, submitterName, submitterEmail);
        ResponseData = responseData;
        Status = SubmissionStatus.Draft;
    }

    /// <summary>Draft and Returned reports can be edited by the owning unit.</summary>
    public bool IsEditable => Status is SubmissionStatus.Draft or SubmissionStatus.Returned;

    public void SaveDraft(string responseData, Guid submitterId, string submitterChandaNo, string submitterName, string? submitterEmail)
    {
        if (!IsEditable)
        {
            throw new InvalidOperationException(Status == SubmissionStatus.Submitted
                ? "This report has been submitted. Withdraw it before editing."
                : $"A report that is {Status} cannot be edited.");
        }

        ResponseData = responseData;
        SetSubmitter(submitterId, submitterChandaNo, submitterName, submitterEmail);
    }

    /// <summary>Submit or resubmit. Lateness is decided by the window, using the server clock.</summary>
    public void Submit(DateTime nowUtc, bool isLate)
    {
        if (!IsEditable)
        {
            throw new InvalidOperationException($"A report that is {Status} cannot be submitted.");
        }

        if (Status == SubmissionStatus.Returned && RevisionDueAt.HasValue && nowUtc > RevisionDueAt.Value)
        {
            throw new InvalidOperationException("The revision deadline for this returned report has passed.");
        }

        // A resubmission after a return keeps the original lateness.
        IsLate = Status == SubmissionStatus.Returned ? IsLate : isLate;
        Status = SubmissionStatus.Submitted;
        SubmittedAt = nowUtc;
        ReviewStartedAt = null;
    }

    /// <summary>The reference is issued once; resubmissions keep it.</summary>
    public void AssignReferenceNumber(string referenceNumber)
    {
        ReferenceNumber ??= referenceNumber;
    }

    /// <summary>Pull a submitted report back to draft, only while the Secretariat has not opened it.</summary>
    public void Withdraw()
    {
        if (Status != SubmissionStatus.Submitted)
        {
            throw new InvalidOperationException("Only a submitted report can be withdrawn.");
        }

        if (ReviewStartedAt.HasValue)
        {
            throw new InvalidOperationException("This report is already being reviewed and can no longer be withdrawn.");
        }

        Status = SubmissionStatus.Draft;
        SubmittedAt = null;
        IsLate = false;
    }

    /// <summary>The Secretariat opened the report. Idempotent.</summary>
    public void StartReview(DateTime nowUtc)
    {
        if (Status != SubmissionStatus.Submitted)
        {
            return;
        }

        Status = SubmissionStatus.InReview;
        ReviewStartedAt = nowUtc;
    }

    public void Acknowledge(Guid reviewerId, string reviewerName, DateTime nowUtc, string? comments = null)
    {
        if (Status is not (SubmissionStatus.Submitted or SubmissionStatus.InReview))
        {
            throw new InvalidOperationException("Only a submitted report can be acknowledged.");
        }

        _approvals.Add(new SubmissionApproval(Id, reviewerId, reviewerName, ApprovalStatus.Acknowledged, comments));
        Status = SubmissionStatus.Acknowledged;
        ReviewStartedAt ??= nowUtc;
        AcknowledgedAt = nowUtc;
    }

    public void Return(Guid reviewerId, string reviewerName, string reason, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("A reason is required to return a report.");
        }

        if (Status is not (SubmissionStatus.Submitted or SubmissionStatus.InReview))
        {
            throw new InvalidOperationException("Only a submitted report can be returned.");
        }

        _approvals.Add(new SubmissionApproval(Id, reviewerId, reviewerName, ApprovalStatus.Returned, reason));
        Status = SubmissionStatus.Returned;
        ReviewStartedAt ??= nowUtc;
        ReturnedAt = nowUtc;
        ReturnReason = reason.Trim();
        RevisionDueAt = nowUtc + RevisionPeriod;
    }

    /// <summary>Reopen an acknowledged report for further review. Secretariat only, kept in history.</summary>
    public void Reopen(Guid reviewerId, string reviewerName, string reason, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("A reason is required to reopen a report.");
        }

        if (Status != SubmissionStatus.Acknowledged)
        {
            throw new InvalidOperationException("Only an acknowledged report can be reopened.");
        }

        _approvals.Add(new SubmissionApproval(Id, reviewerId, reviewerName, ApprovalStatus.Reopened, reason));
        Status = SubmissionStatus.InReview;
        AcknowledgedAt = null;
        ReviewStartedAt = nowUtc;
    }

    /// <summary>The window closed without a submission. Idempotent.</summary>
    public void MarkMissed()
    {
        if (Status == SubmissionStatus.Draft)
        {
            Status = SubmissionStatus.Missed;
        }
    }

    public void AddFileAttachment(Guid questionId, string fileName, string contentType, byte[] fileData, string? description = null)
    {
        var attachment = new FileAttachment(Id, questionId, fileName, contentType, fileData, description);
        _fileAttachments.Add(attachment);
    }

    public void RemoveFileAttachment(Guid attachmentId)
    {
        var attachment = _fileAttachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment != null)
        {
            _fileAttachments.Remove(attachment);
        }
    }

    private void SetSubmitter(Guid submitterId, string submitterChandaNo, string submitterName, string? submitterEmail)
    {
        SubmitterId = submitterId;
        SubmitterChandaNo = submitterChandaNo;
        SubmitterName = submitterName;
        SubmitterEmail = submitterEmail;
    }
}
