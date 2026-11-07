using ManagementApi.Domain.Enums;

namespace ManagementApi.Application.Reports.DTOs;

public record ReportTemplateDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public string ReportType { get; init; } = default!;
    public Guid? ReportTypeId { get; init; }
    public string? ReportTypeCode { get; init; }
    public int Version { get; init; } = 1;
    public Guid VersionGroupId { get; init; }
    public DateTime? PublishedAt { get; init; }
    public string VersionStatus { get; init; } = "Current"; // Draft, Current, Retired
    public List<OrganizationLevel> Levels { get; init; } = new();
    public int QuestionCount { get; init; }
    public int RequiredQuestionCount { get; init; }
    public OrganizationLevel OrganizationLevel { get; init; }
    public bool IsForAllMembers { get; init; }
    public bool IsActive { get; init; }
    public int DisplayOrder { get; init; }
    public List<ReportSectionDto> Sections { get; init; } = new();
    public DateTime CreatedOn { get; init; }
}

public record ReportSectionDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = default!;
    public string? Description { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsRequired { get; init; }
    public List<ReportQuestionDto> Questions { get; init; } = new();
}

public record ReportQuestionDto
{
    public Guid Id { get; init; }
    public string QuestionText { get; init; } = default!;
    public string? HelpText { get; init; }
    public string QuestionType { get; init; } = default!; // Text, Number, Dropdown, etc.
    public string? Options { get; init; } // JSON array for dropdown/radio/checkbox options
    public bool IsRequired { get; init; }
    public int DisplayOrder { get; init; }
    public string? ValidationRules { get; init; }
}

public record CreateReportTemplateRequest
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public string ReportType { get; init; } = default!;
    public OrganizationLevel OrganizationLevel { get; init; }
    public bool IsForAllMembers { get; init; }
    public int DisplayOrder { get; init; }
    public List<CreateReportSectionRequest> Sections { get; init; } = new();

    /// <summary>Levels whose heads fill this template. Empty means just OrganizationLevel.</summary>
    public List<OrganizationLevel>? Levels { get; init; }

    /// <summary>Optional report type to link (its name becomes ReportType).</summary>
    public Guid? ReportTypeId { get; init; }

    /// <summary>False keeps it as a draft to finish later. Defaults to publishing straight away.</summary>
    public bool Publish { get; init; } = true;
}

public record CreateReportSectionRequest
{
    public string Title { get; init; } = default!;
    public string? Description { get; init; }
    public int DisplayOrder { get; init; }
    public bool IsRequired { get; init; } = true;
    public List<CreateReportQuestionRequest> Questions { get; init; } = new();
}

public record CreateReportQuestionRequest
{
    public string QuestionText { get; init; } = default!;
    public string? HelpText { get; init; }
    public string QuestionType { get; init; } = default!;
    public string? Options { get; init; }
    public bool IsRequired { get; init; } = true;
    public int DisplayOrder { get; init; }
    public string? ValidationRules { get; init; }
}

public record UpdateReportTemplateRequest
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public string ReportType { get; init; } = default!;
    public OrganizationLevel OrganizationLevel { get; init; }
    public bool IsForAllMembers { get; init; }
    public int DisplayOrder { get; init; }
}
