namespace HRS.Domain.Admin;

/// <summary>
/// FR-ADM-001's organization-wide settings — a single row. Branding (name, logo, colour) shows in
/// the app shell, PDFs and Word exports; the time zone and date format decide how every date is
/// shown (times are always stored in UTC); the fiscal year start drives yearly views.
/// </summary>
public sealed class OrganizationSettings
{
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-0000000000f1");

    public const int MaxNameLength = 120;

    private OrganizationSettings()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? LogoStorageKey { get; private set; }

    public string? LogoContentType { get; private set; }

    public BrandColor BrandColor { get; private set; }

    /// <summary>An IANA time zone id, e.g. Africa/Lagos (the default).</summary>
    public string TimeZone { get; private set; } = "Africa/Lagos";

    public DateFormatStyle DateFormat { get; private set; }

    /// <summary>1 = January … 12 = December.</summary>
    public int FiscalYearStartMonth { get; private set; } = 1;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public static OrganizationSettings CreateDefault(string name) =>
        new()
        {
            Id = SingletonId,
            Name = name,
            BrandColor = BrandColor.Green,
            TimeZone = "Africa/Lagos",
            DateFormat = DateFormatStyle.DayMonthYear,
            FiscalYearStartMonth = 1,
        };

    /// <summary>The time zone must be a valid IANA id — the caller checks it against the system's
    /// time zone database, which the domain has no access to.</summary>
    public void Update(
        string name, BrandColor brandColor, string timeZone, DateFormatStyle dateFormat, int fiscalYearStartMonth, Guid userId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
        {
            throw new ArgumentException($"The name must be 1–{MaxNameLength} characters.", nameof(name));
        }

        if (fiscalYearStartMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(fiscalYearStartMonth), "The fiscal year starts in a month from 1 to 12.");
        }

        Name = name.Trim();
        BrandColor = brandColor;
        TimeZone = timeZone;
        DateFormat = dateFormat;
        FiscalYearStartMonth = fiscalYearStartMonth;
        Touch(userId, now);
    }

    public void SetLogo(string storageKey, string contentType, Guid userId, DateTimeOffset now)
    {
        LogoStorageKey = storageKey;
        LogoContentType = contentType;
        Touch(userId, now);
    }

    public void RemoveLogo(Guid userId, DateTimeOffset now)
    {
        LogoStorageKey = null;
        LogoContentType = null;
        Touch(userId, now);
    }

    private void Touch(Guid userId, DateTimeOffset now)
    {
        UpdatedAt = now;
        UpdatedByUserId = userId;
    }
}
