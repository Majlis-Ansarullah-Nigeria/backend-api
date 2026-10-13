namespace HRS.Domain.Notifications;

/// <summary>
/// The in-app notification-centre row a user actually sees at `/notifications` (SRS §8's
/// `Notification | ... | Delivery log`) — distinct from <see cref="OutboxMessage"/>, which is
/// purely the reliability/retry mechanism for email. One row per dispatched outbox message
/// (OutboxMessageId is unique), created idempotently regardless of whether that message's email
/// leg succeeded, since the in-app channel doesn't share email's retry semantics.
/// </summary>
public sealed class Notification
{
    private Notification()
    {
    }

    private Notification(
        Guid id, Guid userId, NotificationEventType eventType, string subject, string body, string? link,
        Guid outboxMessageId, bool isBanner, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        EventType = eventType;
        Subject = subject;
        Body = body;
        Link = link;
        OutboxMessageId = outboxMessageId;
        IsBanner = isBanner;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public NotificationEventType EventType { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public string? Link { get; private set; }

    public Guid OutboxMessageId { get; private set; }

    /// <summary>FR-NOT-006: this row's originating Announcement asked to also show as a banner
    /// on Head Home. Only ever true for EventType == Announcement; carried per-message rather
    /// than looked up from the Announcement row so the Home banner query stays a single index
    /// lookup on this table.</summary>
    public bool IsBanner { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public static Notification Create(
        Guid id, Guid userId, NotificationEventType eventType, string subject, string body, string? link,
        Guid outboxMessageId, bool isBanner, DateTimeOffset now) =>
        new(id, userId, eventType, subject, body, link, outboxMessageId, isBanner, now);

    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;
}
