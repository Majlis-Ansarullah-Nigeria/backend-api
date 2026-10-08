using HRS.Domain.Windows;

namespace HRS.Domain.UnitTests.Windows;

public class WindowReminderSettingsTests
{
    [Fact]
    public void DefaultMatchesTheCreateWindowMockupsInitialToggleState()
    {
        var settings = WindowReminderSettings.Default();

        Assert.True(settings.RemindFiveDaysBefore);
        Assert.True(settings.RemindTwoDaysBefore);
        Assert.True(settings.RemindOnDueDay);
        Assert.True(settings.EscalateToParentAfterThreeDays);
        Assert.False(settings.EscalateToZoneAfterSevenDays);
    }
}
