using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Admin.Settings;
using HRS.Domain.Admin;

namespace HRS.Application.UnitTests.Features.Admin;

public class OrganizationBrandingTests
{
    private static OrganizationBranding Branding(string zone, DateFormatStyle format) =>
        OrganizationBranding.Default with { TimeZone = OrganizationBranding.Resolve(zone), DateFormat = format };

    // 23:30 UTC on 4 Oct is already 5 Oct in Lagos (UTC+1).
    private static readonly DateTimeOffset LateEvening = new(2026, 10, 4, 23, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(DateFormatStyle.DayMonthYear, "5 Oct 2026")]
    [InlineData(DateFormatStyle.MonthDayYear, "Oct 5, 2026")]
    [InlineData(DateFormatStyle.Iso, "2026-10-05")]
    public void DatesShowInTheOrganizationsTimeZoneAndFormat(DateFormatStyle format, string expected)
    {
        Assert.Equal(expected, Branding("Africa/Lagos", format).Date(LateEvening));
    }

    [Fact]
    public void DateTimesCarryTheOffset()
    {
        Assert.Equal("5 Oct 2026, 00:30 (UTC+1)", Branding("Africa/Lagos", DateFormatStyle.DayMonthYear).DateTime(LateEvening));
        Assert.Equal("4 Oct 2026, 23:30 (UTC)", Branding("Etc/UTC", DateFormatStyle.DayMonthYear).DateTime(LateEvening));
        Assert.Equal("UTC+5:30", OrganizationBranding.OffsetLabel(OrganizationBranding.Resolve("Asia/Kolkata"), LateEvening));
    }

    [Fact]
    public void AnUnknownTimeZoneFallsBackToUtc()
    {
        Assert.Equal(TimeZoneInfo.Utc, OrganizationBranding.Resolve("Not/AZone"));
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "image/png")]
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67, 0x20 }, "image/svg+xml")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38 }, null)]
    public void OnlyPngAndSvgLogosAreAccepted(byte[] content, string? expected)
    {
        Assert.Equal(expected, UploadLogoCommandHandler.DetectContentType(content));
    }

    [Fact]
    public void AnXmlFileIsOnlyAnSvgIfItHasAnSvgElement()
    {
        Assert.Equal("image/svg+xml", UploadLogoCommandHandler.DetectContentType("<?xml version=\"1.0\"?><svg/>"u8.ToArray()));
        Assert.Null(UploadLogoCommandHandler.DetectContentType("<?xml version=\"1.0\"?><html/>"u8.ToArray()));
    }
}
