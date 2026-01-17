using ManagementApi.Domain.Common;
using ManagementApi.Domain.Enums;

namespace ManagementApi.Domain.Entities.Reports;

/// <summary>
/// Represents a question within a report section
/// </summary>
public class ReportQuestion : AuditableEntity
{
    public Guid ReportSectionId { get; private set; }
    public string QuestionText { get; private set; } = default!;
    public string? HelpText { get; private set; }
    public QuestionType QuestionType { get; private set; }
    public string? Options { get; private set; } // JSON array for dropdown/radio/checkbox options
    public bool IsRequired { get; private set; } = true;
    public int DisplayOrder { get; private set; }
    public string? ValidationRules { get; private set; } // JSON object for validation rules (min, max, regex, etc.)

    // Navigation properties
    public ReportSection ReportSection { get; private set; } = default!;

    private ReportQuestion() { } // EF Core

    public ReportQuestion(
        Guid reportSectionId,
        string questionText,
        QuestionType questionType,
        string? helpText = null,
        string? options = null,
        bool isRequired = true,
        int displayOrder = 0,
        string? validationRules = null)
    {
        ReportSectionId = reportSectionId;
        QuestionText = questionText;
        QuestionType = questionType;
        HelpText = helpText;
        Options = options;
        IsRequired = isRequired;
        DisplayOrder = displayOrder;
        ValidationRules = validationRules;
    }

    public void UpdateDetails(
        string questionText,
        QuestionType questionType,
        string? helpText = null,
        string? options = null,
        bool isRequired = true,
        int displayOrder = 0,
        string? validationRules = null)
    {
        QuestionText = questionText;
        QuestionType = questionType;
        HelpText = helpText;
        Options = options;
        IsRequired = isRequired;
        DisplayOrder = displayOrder;
        ValidationRules = validationRules;
    }
}
