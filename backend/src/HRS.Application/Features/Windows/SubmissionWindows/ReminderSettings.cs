using HRS.Domain.Windows;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed record ReminderSettings(
    bool RemindFiveDaysBefore,
    bool RemindTwoDaysBefore,
    bool RemindOnDueDay,
    bool EscalateToParentAfterThreeDays,
    bool EscalateToZoneAfterSevenDays)
{
    public static ReminderSettings FromEntity(WindowReminderSettings settings) => new(
        settings.RemindFiveDaysBefore,
        settings.RemindTwoDaysBefore,
        settings.RemindOnDueDay,
        settings.EscalateToParentAfterThreeDays,
        settings.EscalateToZoneAfterSevenDays);

    public WindowReminderSettings ToEntity() => new(
        RemindFiveDaysBefore,
        RemindTwoDaysBefore,
        RemindOnDueDay,
        EscalateToParentAfterThreeDays,
        EscalateToZoneAfterSevenDays);
}
