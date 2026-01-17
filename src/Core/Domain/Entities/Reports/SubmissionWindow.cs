using ManagementApi.Domain.Common;

namespace ManagementApi.Domain.Entities.Reports;

/// <summary>
/// Defines the time window during which a report can be submitted
/// </summary>
public class SubmissionWindow : AuditableEntity, IAggregateRoot
{
    public Guid ReportTemplateId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Navigation properties
    public ReportTemplate ReportTemplate { get; private set; } = default!;

    private readonly List<ReportSubmission> _submissions = new();
    public IReadOnlyCollection<ReportSubmission> Submissions => _submissions.AsReadOnly();

    private SubmissionWindow() { } // EF Core

    public SubmissionWindow(Guid reportTemplateId, string name, DateTime startDate, DateTime endDate, string? description = null)
    {
        if (endDate <= startDate)
        {
            throw new ArgumentException("End date must be after start date");
        }

        ReportTemplateId = reportTemplateId;
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        Description = description;
    }

    public void UpdateWindow(string name, DateTime startDate, DateTime endDate, string? description = null)
    {
        if (endDate <= startDate)
        {
            throw new ArgumentException("End date must be after start date");
        }

        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        Description = description;
    }

    public bool IsOpen()
    {
        var now = DateTime.UtcNow;
        return IsActive && now >= StartDate && now <= EndDate;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
