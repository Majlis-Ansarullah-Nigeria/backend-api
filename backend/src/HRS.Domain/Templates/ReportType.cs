using HRS.Domain.Organization;

namespace HRS.Domain.Templates;

/// <summary>
/// What units are asked to submit, who submits it, and where it routes (FR-TPL-001). The
/// template itself — sections, fields, versioning — is M3.2; a ReportType exists independently
/// of whether it has a published template yet.
/// </summary>
public sealed class ReportType
{
    private readonly List<UnitLevelCode> _applicableLevels = [];

    private ReportType()
    {
    }

    private ReportType(
        Guid id,
        string name,
        string? description,
        ReportTypeCategory category,
        ReportTypeFrequency frequency,
        IEnumerable<UnitLevelCode> applicableLevels,
        UnitLevelCode recipientLevel,
        int observerDepth,
        bool allowAttachments,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Description = description;
        Category = category;
        Frequency = frequency;
        _applicableLevels.AddRange(applicableLevels.Distinct());
        RecipientLevel = recipientLevel;
        ObserverDepth = observerDepth;
        AllowAttachments = allowAttachments;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public ReportTypeCategory Category { get; private set; }

    public ReportTypeFrequency Frequency { get; private set; }

    /// <summary>Which unit levels must submit this report type (for example Muqam, Dila and Zone).</summary>
    public IReadOnlyList<UnitLevelCode> ApplicableLevels => _applicableLevels;

    /// <summary>Routing, stored as data per SRS 3.1 so it can change without a release. The v1
    /// default is always National (the Secretariat), but it's configurable per type.</summary>
    public UnitLevelCode RecipientLevel { get; private set; }

    /// <summary>How many levels up from the submitter the read-only observer chain reaches
    /// (0 = no observer, 1 = immediate parent head, and so on). SRS 3.1.</summary>
    public int ObserverDepth { get; private set; }

    public bool AllowAttachments { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    public static ReportType Create(
        Guid id,
        string name,
        string? description,
        ReportTypeCategory category,
        ReportTypeFrequency frequency,
        IEnumerable<UnitLevelCode> applicableLevels,
        UnitLevelCode recipientLevel,
        int observerDepth,
        bool allowAttachments,
        DateTimeOffset now) =>
        new(id, name, description, category, frequency, applicableLevels, recipientLevel, observerDepth, allowAttachments, now);

    public void Update(
        string name,
        string? description,
        ReportTypeCategory category,
        ReportTypeFrequency frequency,
        IEnumerable<UnitLevelCode> applicableLevels,
        UnitLevelCode recipientLevel,
        int observerDepth,
        bool allowAttachments)
    {
        Name = name;
        Description = description;
        Category = category;
        Frequency = frequency;
        _applicableLevels.Clear();
        _applicableLevels.AddRange(applicableLevels.Distinct());
        RecipientLevel = recipientLevel;
        ObserverDepth = observerDepth;
        AllowAttachments = allowAttachments;
    }

    public void Archive(DateTimeOffset now) => ArchivedAt = now;

    public void Restore() => ArchivedAt = null;
}
