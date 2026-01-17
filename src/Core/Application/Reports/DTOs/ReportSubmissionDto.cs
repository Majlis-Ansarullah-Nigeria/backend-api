using ManagementApi.Domain.Enums;

namespace ManagementApi.Application.Reports.DTOs;

public record ReportSubmissionDto
{
    public Guid Id { get; init; }
    public Guid ReportTemplateId { get; init; }
    public string ReportTemplateName { get; init; } = default!;
    public string SubmitterChandaNo { get; init; } = default!;
    public string SubmitterName { get; init; } = default!;
    public string? SubmitterEmail { get; init; }
    public string OrganizationLevel { get; init; } = default!;
    public string? MuqamName { get; init; }
    public string? DilaName { get; init; }
    public string? ZoneName { get; init; }
    public string ResponseData { get; init; } = default!; // JSON
    public string Status { get; init; } = default!;
    public DateTime? SubmittedAt { get; init; }
    public string? RejectionReason { get; init; }
    public DateTime CreatedOn { get; init; }
    public List<SubmissionApprovalDto> Approvals { get; init; } = new();
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
    public string ResponseData { get; init; } = default!; // JSON with question IDs as keys
}

public record UpdateDraftRequest
{
    public Guid SubmissionId { get; init; }
    public string ResponseData { get; init; } = default!;
}

public record ApproveSubmissionRequest
{
    public Guid SubmissionId { get; init; }
    public string? Comments { get; init; }
}

public record RejectSubmissionRequest
{
    public Guid SubmissionId { get; init; }
    public string Reason { get; init; } = default!;
}

public record GetSubmissionsRequest
{
    public Guid? ReportTemplateId { get; init; }
    public SubmissionStatus? Status { get; init; }
    public Guid? MuqamId { get; init; }
    public Guid? DilaId { get; init; }
    public Guid? ZoneId { get; init; }
    public OrganizationLevel? OrganizationLevel { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}
