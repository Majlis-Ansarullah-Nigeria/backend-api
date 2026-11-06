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
        bool allowNilReport,
        string code,
        DateTimeOffset createdAt)
    {
        Id = id;
        Code = NormalizeCode(code);
        Name = name;
        Description = description;
        Category = category;
        Frequency = frequency;
        _applicableLevels.AddRange(applicableLevels.Distinct());
        RecipientLevel = recipientLevel;
        ObserverDepth = observerDepth;
        AllowAttachments = allowAttachments;
        AllowNilReport = allowNilReport;
        CreatedAt = createdAt;
    }

    public const int MaxCodeLength = 6;

    public Guid Id { get; private set; }

    /// <summary>
    /// The short code that starts this type's reference numbers (FR-SUB-010:
    /// <c>{TYPECODE}-{period}-{unit}</c>), e.g. GMR. Unique across report types, so two monthly
    /// types reporting for the same unit in the same month never share a reference.
    /// </summary>
    public string Code { get; private set; } = string.Empty;

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

    /// <summary>FR-SUB-012's "where the template allows it" — a one-click "No activity this
    /// period" submission, opt-in per report type like AllowAttachments.</summary>
    public bool AllowNilReport { get; private set; }

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
        bool allowNilReport,
        string code,
        DateTimeOffset now) =>
        new(id, name, description, category, frequency, applicableLevels, recipientLevel, observerDepth, allowAttachments, allowNilReport, code, now);

    /// <summary>2–6 capital letters or digits, starting with a letter.</summary>
    public static bool IsValidCode(string? code) =>
        code is not null && code.Length is >= 2 and <= MaxCodeLength && char.IsAsciiLetterUpper(code[0]) && code.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c));

    /// <summary>Changes the reference-number code. Callers must only allow this before any report
    /// of this type has been submitted, so references already issued keep one format.</summary>
    public void ChangeCode(string code) => Code = NormalizeCode(code);

    private static string NormalizeCode(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (!IsValidCode(normalized))
        {
            throw new ArgumentException("A report type code is 2–6 capital letters or digits, starting with a letter.", nameof(code));
        }

        return normalized;
    }

    public void Update(
        string name,
        string? description,
        ReportTypeCategory category,
        ReportTypeFrequency frequency,
        IEnumerable<UnitLevelCode> applicableLevels,
        UnitLevelCode recipientLevel,
        int observerDepth,
        bool allowAttachments,
        bool allowNilReport)
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
        AllowNilReport = allowNilReport;
    }

    public void Archive(DateTimeOffset now) => ArchivedAt = now;

    public void Restore() => ArchivedAt = null;
}
