namespace ManagementApi.Application.Reports.DTOs;

public record SubmissionWindowDto
{
    public Guid Id { get; init; }
    public Guid ReportTemplateId { get; init; }
    public string ReportTemplateName { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public bool IsActive { get; init; }
    public bool IsOpen { get; init; } // Computed: IsActive && StartDate <= Now <= EndDate
    public int SubmissionCount { get; init; }
    public DateTime CreatedOn { get; init; }
}

public record CreateSubmissionWindowRequest
{
    public Guid ReportTemplateId { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
}

public record UpdateSubmissionWindowRequest
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
}
