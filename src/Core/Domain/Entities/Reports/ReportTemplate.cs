using ManagementApi.Domain.Common;
using ManagementApi.Domain.Enums;

namespace ManagementApi.Domain.Entities.Reports;

/// <summary>
/// Represents a template for a report with sections and questions
/// </summary>
public class ReportTemplate : AuditableEntity, IAggregateRoot
{
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string ReportType { get; private set; } = default!; // e.g., "Monthly", "Quarterly", "Annual"
    public OrganizationLevel OrganizationLevel { get; private set; } // Which level this template is for
    public bool IsForAllMembers { get; private set; } // If true, every member can submit this report
    public bool IsActive { get; private set; } = true;
    public int DisplayOrder { get; private set; }

    // The report type this template belongs to (its code starts reference numbers)
    public Guid? ReportTypeId { get; private set; }
    public ReportType? ReportTypeEntity { get; private set; }

    // Versions: a published version is never edited. A change is a new version that retires the
    // earlier ones; reports keep pointing at the version they were filled against.
    public int Version { get; private set; } = 1;
    public Guid? RootTemplateId { get; private set; } // the first version; null on the first version itself

    public Guid VersionGroupId => RootTemplateId ?? Id;

    private readonly List<ReportSection> _sections = new();
    public IReadOnlyCollection<ReportSection> Sections => _sections.AsReadOnly();

    private ReportTemplate() { } // EF Core

    public ReportTemplate(
        string name,
        string reportType,
        OrganizationLevel organizationLevel,
        bool isForAllMembers = false,
        string? description = null,
        int displayOrder = 0)
    {
        Name = name;
        ReportType = reportType;
        OrganizationLevel = organizationLevel;
        IsForAllMembers = isForAllMembers;
        Description = description;
        DisplayOrder = displayOrder;
    }

    public void UpdateDetails(
        string name,
        string reportType,
        OrganizationLevel organizationLevel,
        bool isForAllMembers = false,
        string? description = null,
        int displayOrder = 0)
    {
        Name = name;
        ReportType = reportType;
        OrganizationLevel = organizationLevel;
        IsForAllMembers = isForAllMembers;
        Description = description;
        DisplayOrder = displayOrder;
    }

    public void AddSection(ReportSection section)
    {
        _sections.Add(section);
    }

    public void RemoveSection(Guid sectionId)
    {
        var section = _sections.FirstOrDefault(s => s.Id == sectionId);
        if (section != null)
        {
            _sections.Remove(section);
        }
    }

    /// <summary>Makes this template the next version after <paramref name="previous"/> and retires the earlier one.</summary>
    public void BecomeNextVersionOf(ReportTemplate previous, int version)
    {
        RootTemplateId = previous.VersionGroupId;
        Version = version;
        ReportTypeId = previous.ReportTypeId;
        ReportType = previous.ReportType;
        Activate();
    }

    public void AssignReportType(ReportType reportType)
    {
        ReportTypeId = reportType.Id;
        ReportType = reportType.Name;
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
