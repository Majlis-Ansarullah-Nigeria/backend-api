using HRS.Domain.Admin;
using HRS.Domain.Notifications;

namespace HRS.Domain.UnitTests.Admin;

public class OrganizationSettingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UpdateTrimsTheNameAndRecordsWhoChangedIt()
    {
        var settings = OrganizationSettings.CreateDefault("Default");
        var userId = Guid.NewGuid();

        settings.Update("  Majlis  ", BrandColor.Blue, "Europe/London", DateFormatStyle.Iso, 4, userId, Now);

        Assert.Equal(("Majlis", BrandColor.Blue, "Europe/London", DateFormatStyle.Iso, 4), (settings.Name, settings.BrandColor, settings.TimeZone, settings.DateFormat, settings.FiscalYearStartMonth));
        Assert.Equal((Now, userId), (settings.UpdatedAt!.Value, settings.UpdatedByUserId!.Value));
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("Name", 0)]
    [InlineData("Name", 13)]
    public void UpdateRefusesAnEmptyNameOrAMonthOutOfRange(string name, int month)
    {
        var settings = OrganizationSettings.CreateDefault("Default");

        Assert.ThrowsAny<ArgumentException>(() => settings.Update(name, BrandColor.Green, "Africa/Lagos", DateFormatStyle.DayMonthYear, month, Guid.NewGuid(), Now));
    }

    [Fact]
    public void ALogoCanBeSetAndRemoved()
    {
        var settings = OrganizationSettings.CreateDefault("Default");

        settings.SetLogo("branding/logo.png", "image/png", Guid.NewGuid(), Now);
        Assert.Equal("branding/logo.png", settings.LogoStorageKey);
        settings.RemoveLogo(Guid.NewGuid(), Now);
        Assert.Null(settings.LogoStorageKey);
    }

    [Fact]
    public void EveryBrandColourHasAPalette()
    {
        foreach (var color in Enum.GetValues<BrandColor>())
        {
            var palette = BrandColors.For(color);
            Assert.Matches("^#[0-9A-F]{6}$", palette.Primary);
            Assert.Matches("^#[0-9A-F]{6}$", palette.Dark);
        }
    }

    [Fact]
    public void AGivenUpNotificationCanBeRetriedButASentOneCannot()
    {
        var message = OutboxMessage.Create(Guid.NewGuid(), NotificationEventType.Reminder, Guid.NewGuid(), "{}", Now);
        for (var i = 0; i < 5; i++)
        {
            message.RecordFailure("SMTP down");
        }

        message.ResetForRetry();
        Assert.Equal(0, message.Attempts);

        message.MarkSent(Now);
        Assert.Throws<InvalidOperationException>(message.ResetForRetry);
    }
}
