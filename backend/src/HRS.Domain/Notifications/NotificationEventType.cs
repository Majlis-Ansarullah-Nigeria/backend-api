namespace HRS.Domain.Notifications;

/// <summary>FR-NOT-002's literal event list (WindowOpened..TemplateChanged), plus three from
/// later, separately-numbered requirements: Escalated (FR-NOT-005 — notifies a Muqam's
/// parent/Zone head, never the Muqam's own head WindowJobRunner already notifies via Overdue),
/// Announcement (FR-NOT-006), and ExportReady (FR-HIS-004 — a bulk export finished in the
/// background). Window Closed (BR-WIN-08, WindowJobEventType) still isn't in this list and stays
/// pure WindowJobRun bookkeeping — no notification.</summary>
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
    Escalated,
    Announcement,
    ExportReady,
}
