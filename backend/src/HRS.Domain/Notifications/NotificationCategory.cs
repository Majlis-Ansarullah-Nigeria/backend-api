namespace HRS.Domain.Notifications;

/// <summary>FR-NOT-004's preference granularity — the Profile mockup's 5 toggle rows, coarser
/// than NotificationEventType. ChildSubmitted and TemplateChanged have no category and stay
/// ungated (always both channels): the mockup draws no row for either.</summary>
public enum NotificationCategory
{
    WindowOpened,
    DeadlineReminders,
    ReportStatusChanged,
    Comments,
    Announcements,
}

public static class NotificationCategoryMap
{
    /// <summary>Null means "no preference row applies" — always deliver both channels.</summary>
    public static NotificationCategory? For(NotificationEventType eventType) => eventType switch
    {
        NotificationEventType.WindowOpened => NotificationCategory.WindowOpened,
        NotificationEventType.Reminder or NotificationEventType.Overdue
            or NotificationEventType.ExtensionGranted or NotificationEventType.Escalated => NotificationCategory.DeadlineReminders,
        NotificationEventType.ReturnedForRevision or NotificationEventType.ReportAcknowledged => NotificationCategory.ReportStatusChanged,
        NotificationEventType.CommentAdded => NotificationCategory.Comments,
        NotificationEventType.Announcement => NotificationCategory.Announcements,
        _ => null,
    };
}
