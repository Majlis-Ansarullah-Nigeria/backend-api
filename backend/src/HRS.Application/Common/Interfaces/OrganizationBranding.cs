using System.Globalization;
using HRS.Domain.Admin;

namespace HRS.Application.Common.Interfaces;

/// <summary>
/// FR-ADM-001's settings as documents and messages need them: the organization's name, logo
/// (bytes, only when asked for), brand colours, and how to show a date. Times are stored in UTC
/// and only converted here, at the point of display.
/// </summary>
public sealed record OrganizationBranding(
    string Name,
    byte[]? Logo,
    string? LogoContentType,
    string PrimaryColor,
    string DarkColor,
    TimeZoneInfo TimeZone,
    DateFormatStyle DateFormat)
{
    public const string DefaultName = "Hierarchical Reporting Service";

    public static OrganizationBranding Default { get; } = new(
        DefaultName, null, null, BrandColors.For(BrandColor.Green).Primary, BrandColors.For(BrandColor.Green).Dark,
        Resolve("Africa/Lagos"), DateFormatStyle.DayMonthYear);

    public string Date(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, TimeZone).ToString(DatePattern, CultureInfo.InvariantCulture);

    public string DateTime(DateTimeOffset value) =>
        $"{Date(value)}, {TimeZoneInfo.ConvertTime(value, TimeZone).ToString("HH:mm", CultureInfo.InvariantCulture)} ({OffsetLabel(value)})";

    public string OffsetLabel(DateTimeOffset value) => OffsetLabel(TimeZone, value);

    /// <summary>"UTC+1", "UTC-5:30", "UTC".</summary>
    public static string OffsetLabel(TimeZoneInfo zone, DateTimeOffset value)
    {
        var offset = zone.GetUtcOffset(value);
        if (offset == TimeSpan.Zero)
        {
            return "UTC";
        }

        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        return abs.Minutes == 0
            ? $"UTC{sign}{abs.Hours.ToString(CultureInfo.InvariantCulture)}"
            : $"UTC{sign}{abs.Hours.ToString(CultureInfo.InvariantCulture)}:{abs.Minutes.ToString("00", CultureInfo.InvariantCulture)}";
    }

    private string DatePattern => DateFormat switch
    {
        DateFormatStyle.MonthDayYear => "MMM d, yyyy",
        DateFormatStyle.Iso => "yyyy-MM-dd",
        _ => "d MMM yyyy",
    };

    /// <summary>An IANA id to a TimeZoneInfo, falling back to UTC for an unknown id.</summary>
    public static TimeZoneInfo Resolve(string ianaId) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(ianaId, out var zone) ? zone : TimeZoneInfo.Utc;
}

/// <summary>Loads the organization's branding once per request (or job).</summary>
public interface IOrganizationBrandingProvider
{
    Task<OrganizationBranding> GetAsync(CancellationToken cancellationToken, bool includeLogo = false);
}
