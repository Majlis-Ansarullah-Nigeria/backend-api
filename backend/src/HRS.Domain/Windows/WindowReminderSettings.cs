namespace HRS.Domain.Windows;

/// <summary>
/// Per-window reminder and escalation toggles (FR-NOT-002, FR-NOT-005), owned by SubmissionWindow
/// — the "Create window" mockup's Reminders panel. The escalation thresholds (3 days, 7 days) are
/// fixed, not editable fields — the mockup only ever shows a toggle next to each, never a number
/// input, so there is nothing to store beyond on/off. WindowJobRunner (M4.4) reads these instead
/// of a hardcoded reminder cadence.
/// </summary>
public sealed class WindowReminderSettings
{
    private WindowReminderSettings()
    {
    }

    public WindowReminderSettings(
        bool remindFiveDaysBefore,
        bool remindTwoDaysBefore,
        bool remindOnDueDay,
        bool escalateToParentAfterThreeDays,
        bool escalateToZoneAfterSevenDays)
    {
        RemindFiveDaysBefore = remindFiveDaysBefore;
        RemindTwoDaysBefore = remindTwoDaysBefore;
        RemindOnDueDay = remindOnDueDay;
        EscalateToParentAfterThreeDays = escalateToParentAfterThreeDays;
        EscalateToZoneAfterSevenDays = escalateToZoneAfterSevenDays;
    }

    public bool RemindFiveDaysBefore { get; private set; }

    public bool RemindTwoDaysBefore { get; private set; }

    public bool RemindOnDueDay { get; private set; }

    public bool EscalateToParentAfterThreeDays { get; private set; }

    public bool EscalateToZoneAfterSevenDays { get; private set; }

    /// <summary>The mockup's initial toggle state: every reminder on, only the zone escalation off.</summary>
    public static WindowReminderSettings Default() => new(
        remindFiveDaysBefore: true,
        remindTwoDaysBefore: true,
        remindOnDueDay: true,
        escalateToParentAfterThreeDays: true,
        escalateToZoneAfterSevenDays: false);
}
