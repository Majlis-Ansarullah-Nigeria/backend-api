namespace HRS.Domain.Notifications;

/// <summary>
/// A notification queued durably in the same transaction as the event that caused it (SRS §7.3:
/// "domain events are stored in outbox_messages in the same transaction and dispatched by the
/// worker"). PayloadJson is a flat string/string dictionary of placeholder values (FR-NOT-003) —
/// deliberately opaque here, the same reasoning AnswersJson/SchemaJson use elsewhere: the shape
/// belongs to the Application layer that writes and reads it, not Domain. RecipientUserId is
/// resolved once, at enqueue time (who was the active unit head *then*); the dispatcher still
/// looks up that user's current email fresh at send time, so a deactivated account just fails
/// that one send rather than silently mailing someone who no longer holds the role.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    private OutboxMessage(Guid id, NotificationEventType eventType, Guid recipientUserId, string payloadJson, DateTimeOffset now)
    {
        Id = id;
        EventType = eventType;
        RecipientUserId = recipientUserId;
        PayloadJson = payloadJson;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public NotificationEventType EventType { get; private set; }

    public Guid RecipientUserId { get; private set; }

    public string PayloadJson { get; private set; } = "{}";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage Create(Guid id, NotificationEventType eventType, Guid recipientUserId, string payloadJson, DateTimeOffset now) =>
        new(id, eventType, recipientUserId, payloadJson, now);

    public void MarkSent(DateTimeOffset now)
    {
        ProcessedAt = now;
        LastError = null;
    }

    public void RecordFailure(string error)
    {
        Attempts++;
        LastError = error;
    }

    /// <summary>FR-ADM-005's "Retry failed notifications": a message the dispatcher gave up on
    /// gets a fresh set of attempts. Already-sent messages are never resent.</summary>
    public void ResetForRetry()
    {
        if (ProcessedAt is not null)
        {
            throw new InvalidOperationException("A sent notification can't be retried.");
        }

        Attempts = 0;
    }
}
