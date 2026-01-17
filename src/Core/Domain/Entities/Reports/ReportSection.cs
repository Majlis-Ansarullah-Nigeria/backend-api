using ManagementApi.Domain.Common;

namespace ManagementApi.Domain.Entities.Reports;

/// <summary>
/// Represents a section within a report template
/// </summary>
public class ReportSection : AuditableEntity
{
    public Guid ReportTemplateId { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsRequired { get; private set; } = true;

    private readonly List<ReportQuestion> _questions = new();
    public IReadOnlyCollection<ReportQuestion> Questions => _questions.AsReadOnly();

    // Navigation properties
    public ReportTemplate ReportTemplate { get; private set; } = default!;

    private ReportSection() { } // EF Core

    public ReportSection(Guid reportTemplateId, string title, string? description = null, int displayOrder = 0, bool isRequired = true)
    {
        ReportTemplateId = reportTemplateId;
        Title = title;
        Description = description;
        DisplayOrder = displayOrder;
        IsRequired = isRequired;
    }

    public void UpdateDetails(string title, string? description = null, int displayOrder = 0, bool isRequired = true)
    {
        Title = title;
        Description = description;
        DisplayOrder = displayOrder;
        IsRequired = isRequired;
    }

    public void AddQuestion(ReportQuestion question)
    {
        _questions.Add(question);
    }

    public void RemoveQuestion(Guid questionId)
    {
        var question = _questions.FirstOrDefault(q => q.Id == questionId);
        if (question != null)
        {
            _questions.Remove(question);
        }
    }
}
