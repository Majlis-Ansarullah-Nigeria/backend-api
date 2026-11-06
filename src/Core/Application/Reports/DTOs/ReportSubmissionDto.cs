using ManagementApi.Application.Reports.Common;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Enums;

namespace ManagementApi.Application.Reports.DTOs;

public record ReportSubmissionDto
{
    public Guid Id { get; init; }
    public Guid ReportTemplateId { get; init; }
    public string ReportTemplateName { get; init; } = default!;
    public Guid? SubmissionWindowId { get; init; }
    public string SubmitterChandaNo { get; init; } = default!;
    public string SubmitterName { get; init; } = default!;
    public string? SubmitterEmail { get; init; }
    public string OrganizationLevel { get; init; } = default!;
    public Guid UnitId { get; init; }
    public string? MuqamName { get; init; }
    public string? DilaName { get; init; }
    public string? ZoneName { get; init; }
    public string ResponseData { get; init; } = default!; // JSON
    public string Status { get; init; } = default!;
    public DateTime? SubmittedAt { get; init; }
    public bool IsLate { get; init; }
    public string? ReferenceNumber { get; init; }
    public DateTime? ReviewStartedAt { get; init; }
    public DateTime? AcknowledgedAt { get; init; }
    public DateTime? ReturnedAt { get; init; }
    public string? ReturnReason { get; init; }
    public DateTime? RevisionDueAt { get; init; }
    public DateTime CreatedOn { get; init; }
    public List<SubmissionApprovalDto> Approvals { get; init; } = new();

    // What the current user may do with this report. The API enforces the same rules.
    public bool IsOwnUnit { get; init; }
    public bool CanEdit { get; init; }
    public bool CanWithdraw { get; init; }
    public bool CanReview { get; init; }
    public bool CanComment { get; init; } // Secretariat and owning unit; never observers

    public static ReportSubmissionDto From(ReportSubmission s, ICurrentUserService user) => new()
    {
        Id = s.Id,
        ReportTemplateId = s.ReportTemplateId,
        ReportTemplateName = s.ReportTemplate?.Name ?? string.Empty,
        SubmissionWindowId = s.SubmissionWindowId,
        SubmitterChandaNo = s.SubmitterChandaNo,
        SubmitterName = s.SubmitterName,
        SubmitterEmail = s.SubmitterEmail,
        OrganizationLevel = s.OrganizationLevel.ToString(),
        UnitId = s.UnitId,
        ResponseData = s.ResponseData,
        Status = s.Status.ToString(),
        SubmittedAt = s.SubmittedAt,
        IsLate = s.IsLate,
        ReferenceNumber = s.ReferenceNumber,
        ReviewStartedAt = s.ReviewStartedAt,
        AcknowledgedAt = s.AcknowledgedAt,
        ReturnedAt = s.ReturnedAt,
        ReturnReason = s.ReturnReason,
        RevisionDueAt = s.RevisionDueAt,
        CreatedOn = s.CreatedOn,
        Approvals = s.Approvals
            .OrderBy(a => a.ActionDate)
            .Select(a => new SubmissionApprovalDto
            {
                Id = a.Id,
                ApproverName = a.ApproverName,
                Status = a.Status.ToString(),
                Comments = a.Comments,
                ActionDate = a.ActionDate
            }).ToList(),
        IsOwnUnit = ReportingScope.OwnsReport(user, s),
        CanEdit = ReportingScope.OwnsReport(user, s) && s.IsEditable,
        CanWithdraw = ReportingScope.OwnsReport(user, s) && s.Status == SubmissionStatus.Submitted && s.ReviewStartedAt == null,
        CanComment = ReportingScope.IsSecretariat(user) || ReportingScope.OwnsReport(user, s),
        CanReview = ReportingScope.IsSecretariat(user) &&
            s.Status is SubmissionStatus.Submitted or SubmissionStatus.InReview or SubmissionStatus.Acknowledged
    };
}

public record SubmissionApprovalDto
{
    public Guid Id { get; init; }
    public string ApproverName { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string? Comments { get; init; }
    public DateTime ActionDate { get; init; }
}

public record SubmitReportRequest
{
    public Guid ReportTemplateId { get; init; }
    public Guid? SubmissionWindowId { get; init; }
    public string ResponseData { get; init; } = default!; // JSON with question IDs as keys
}

public record SaveDraftRequest
{
    public Guid ReportTemplateId { get; init; }
    public Guid? SubmissionWindowId { get; init; }
    public string ResponseData { get; init; } = default!; // JSON with question IDs as keys
}

public record AcknowledgeSubmissionRequest
{
    public Guid SubmissionId { get; init; }
    public string? Comments { get; init; }
}

public record ReturnSubmissionRequest
{
    public Guid SubmissionId { get; init; }
    public string Reason { get; init; } = default!;
}

public record ReopenSubmissionRequest
{
    public Guid SubmissionId { get; init; }
    public string Reason { get; init; } = default!;
}

public record GetSubmissionsRequest
{
    public Guid? ReportTemplateId { get; init; }
    public Guid? SubmissionWindowId { get; init; }
    public SubmissionStatus? Status { get; init; }
    public bool? IsLate { get; init; }
    public Guid? MuqamId { get; init; }
    public Guid? DilaId { get; init; }
    public Guid? ZoneId { get; init; }
    public OrganizationLevel? OrganizationLevel { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}
