namespace HRS.Domain.Notifications;

/// <summary>FR-NOT-003: one editable template per event type, with {{placeholder}} tokens
/// ({{headName}}, {{unitName}}, {{dueDate}}, {{link}}, plus a few event-specific ones). Seeded
/// with defaults via a one-time data migration (same pattern as M6.3's default report template) —
/// ordinary Secretariat-editable rows, not permanent schema.</summary>
public sealed class NotificationTemplate
{
    private NotificationTemplate()
    {
    }

    public NotificationTemplate(Guid id, NotificationEventType eventType, string subject, string body, DateTimeOffset now)
    {
        Id = id;
        EventType = eventType;
        Subject = subject;
        Body = body;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public NotificationEventType EventType { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public void Update(string subject, string body, Guid updatedByUserId, DateTimeOffset now)
    {
        Subject = subject;
        Body = body;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = now;
    }
}
