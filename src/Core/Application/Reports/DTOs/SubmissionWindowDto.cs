using ManagementApi.Domain.Enums;

namespace ManagementApi.Application.Reports.DTOs;

public record SubmissionWindowDto
{
    public Guid Id { get; init; }
    public Guid ReportTemplateId { get; init; }
    public string ReportTemplateName { get; init; } = default!;
    public int ReportTemplateVersion { get; init; } = 1;
    public string? ReportTypeCode { get; init; }
    public OrganizationLevel TemplateLevel { get; init; } // lowest level that submits (older screens)
    public List<OrganizationLevel> TemplateLevels { get; init; } = new();
    public DateTime? PeriodStart { get; init; }
    public int ExpectedCount { get; init; }
    public int MissedCount { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public DateTime StartDate { get; init; } // opens
    public DateTime EndDate { get; init; }   // closes
    public DateTime? MuqamDueAt { get; init; }
    public DateTime? DilaDueAt { get; init; }
    public DateTime? ZoneDueAt { get; init; }
    public bool IsActive { get; init; }
    public bool IsOpen { get; init; } // Computed with the server clock
    public string Phase { get; init; } = default!; // Upcoming, Open, Closed, Inactive
    public int SubmissionCount { get; init; }
    public DateTime CreatedOn { get; init; }
    public List<string> Warnings { get; init; } = new();
    public List<WindowExtensionDto> Extensions { get; init; } = new();

    // For the current user's own unit (null for the Secretariat or users without a unit)
    public DateTime? MyDueAt { get; init; }
    public DateTime? MyClosesAt { get; init; }
    public bool? IsOpenForMe { get; init; }
}

public record WindowExtensionDto
{
    public Guid Id { get; init; }
    public string OrganizationLevel { get; init; } = default!;
    public Guid UnitId { get; init; }
    public string? UnitName { get; init; }
    public DateTime DueAt { get; init; }
    public DateTime ClosesAt { get; init; }
    public string Reason { get; init; } = default!;
}

public record CreateSubmissionWindowRequest
{
    public Guid ReportTemplateId { get; init; }
    public DateTime? PeriodStart { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public DateTime? MuqamDueAt { get; init; }
    public DateTime? DilaDueAt { get; init; }
    public DateTime? ZoneDueAt { get; init; }
}

public record UpdateSubmissionWindowRequest
{
    public DateTime? PeriodStart { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public DateTime? MuqamDueAt { get; init; }
    public DateTime? DilaDueAt { get; init; }
    public DateTime? ZoneDueAt { get; init; }
}

public record GrantExtensionRequest
{
    public OrganizationLevel OrganizationLevel { get; init; }
    public Guid UnitId { get; init; }
    public DateTime DueAt { get; init; }
    public DateTime ClosesAt { get; init; }
    public string Reason { get; init; } = default!;
}
