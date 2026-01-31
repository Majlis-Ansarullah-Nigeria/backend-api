using ManagementApi.Domain.Common;

namespace ManagementApi.Domain.Entities.Reports;

/// <summary>
/// Represents a notification sent to a user about report-related events
/// </summary>
public class Notification : AuditableEntity
{
    public NotificationType Type { get; private set; }
    public Guid RecipientId { get; private set; }
    public string RecipientName { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string Message { get; private set; } = default!;
    public Guid? RelatedEntityId { get; private set; }
    public string? RelatedEntityType { get; private set; } // "SubmissionWindow", "ReportSubmission", etc.
    public bool IsRead { get; private set; }
    public DateTime? ReadOn { get; private set; }
    public NotificationPriority Priority { get; private set; }

    // For EF Core
    private Notification() { }

    public Notification(
        NotificationType type,
        Guid recipientId,
        string recipientName,
        string title,
        string message,
        NotificationPriority priority = NotificationPriority.Normal,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null)
    {
        Type = type;
        RecipientId = recipientId;
        RecipientName = recipientName ?? throw new ArgumentNullException(nameof(recipientName));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Priority = priority;
        RelatedEntityId = relatedEntityId;
        RelatedEntityType = relatedEntityType;
        IsRead = false;
    }

    public void MarkAsRead()
    {
        if (!IsRead)
        {
            IsRead = true;
            ReadOn = DateTime.UtcNow;
        }
    }

    public void MarkAsUnread()
    {
        IsRead = false;
        ReadOn = null;
    }
}

/// <summary>
/// Types of notifications in the report management system
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// A new submission window has been opened
    /// </summary>
    WindowOpened = 1,

    /// <summary>
    /// A submission window deadline is approaching (e.g., 3 days remaining)
    /// </summary>
    DeadlineReminder = 2,

    /// <summary>
    /// A submission window has been extended
    /// </summary>
    WindowExtended = 3,

    /// <summary>
    /// A submission window has been closed
    /// </summary>
    WindowClosed = 4,

    /// <summary>
    /// A report submission has been approved
    /// </summary>
    SubmissionApproved = 5,

    /// <summary>
    /// A report submission has been rejected
    /// </summary>
    SubmissionRejected = 6,

    /// <summary>
    /// A comment has been added to a submission
    /// </summary>
    CommentAdded = 7,

    /// <summary>
    /// A subordinate organization has submitted a report
    /// </summary>
    SubordinateSubmission = 8,

    /// <summary>
    /// Multiple submissions have been bulk approved
    /// </summary>
    BulkApproved = 9,

    /// <summary>
    /// Multiple submissions have been bulk rejected
    /// </summary>
    BulkRejected = 10,

    /// <summary>
    /// Overdue submissions detected
    /// </summary>
    OverdueAlert = 11
}

/// <summary>
/// Priority level of a notification
/// </summary>
public enum NotificationPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4
}
