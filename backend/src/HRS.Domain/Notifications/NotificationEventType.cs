namespace HRS.Domain.Notifications;

/// <summary>FR-NOT-002's literal event list. Window Closed/Escalated (BR-WIN-08, WindowJobEventType)
/// aren't in this list and stay pure WindowJobRun bookkeeping — no notification.</summary>
public enum NotificationEventType
{
    WindowOpened,
    Reminder,
    Overdue,
    ExtensionGranted,
    ReportAcknowledged,
    ReturnedForRevision,
    CommentAdded,
    ChildSubmitted,
    TemplateChanged,
}
