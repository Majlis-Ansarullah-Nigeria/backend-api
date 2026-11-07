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

    /// <summary>Null while the version is a draft. A published version is never edited.</summary>
    public DateTime? PublishedAt { get; private set; }

    public bool IsDraft => PublishedAt is null;

    /// <summary>
    /// The levels whose heads fill this template, as flags (Muqam = 2, Dila = 4, Zone = 8).
    /// 0 means "only OrganizationLevel" (templates created before levels were added).
    /// </summary>
    public int SubmittingLevelFlags { get; private set; }

    public IReadOnlyList<OrganizationLevel> SubmittingLevels =>
        SubmittingLevelFlags == 0
            ? new[] { OrganizationLevel }
            : new[] { OrganizationLevel.Muqam, OrganizationLevel.Dila, OrganizationLevel.Zone }
                .Where(l => (SubmittingLevelFlags & (1 << (int)l)) != 0).ToArray();

    public bool IsFilledBy(OrganizationLevel level) => SubmittingLevels.Contains(level);

    public void SetSubmittingLevels(IEnumerable<OrganizationLevel> levels)
    {
        var list = levels.Distinct().Where(l => l is OrganizationLevel.Muqam or OrganizationLevel.Dila or OrganizationLevel.Zone).ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("Choose at least one level that fills this template");
        }

        SubmittingLevelFlags = list.Aggregate(0, (flags, l) => flags | (1 << (int)l));
        OrganizationLevel = list.Min(); // lowest level, kept for older screens
    }

    public void Publish(DateTime nowUtc)
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("This version is already published.");
        }

        if (_sections.Count == 0 || _sections.All(s => s.Questions.Count == 0))
        {
            throw new InvalidOperationException("Add at least one section with a question before publishing.");
        }

        PublishedAt = nowUtc;
        Activate();
    }

    /// <summary>Drafts only: replace name, instructions and the whole structure.</summary>
    public void ReplaceDraft(string name, string? description, IEnumerable<OrganizationLevel> levels)
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("A published version can't be edited. Create a new version instead.");
        }

        Name = name;
        Description = description;
        SetSubmittingLevels(levels);
        _sections.Clear();
    }

    /// <summary>For templates published straight away (the older create screen and seeded templates).</summary>
    public void MarkPublished(DateTime nowUtc) => PublishedAt ??= nowUtc;

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
    /// <summary>Starts this template as a draft of the next version after <paramref name="previous"/>.</summary>
    public void BecomeNextVersionOf(ReportTemplate previous, int version)
    {
        RootTemplateId = previous.VersionGroupId;
        Version = version;
        ReportTypeId = previous.ReportTypeId;
        ReportType = previous.ReportType;
        PublishedAt = null;
        Deactivate(); // a draft can't get windows until it's published
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
