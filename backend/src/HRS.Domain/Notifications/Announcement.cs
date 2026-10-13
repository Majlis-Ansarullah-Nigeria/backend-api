namespace HRS.Domain.Notifications;

/// <summary>FR-NOT-006's broadcast definition and "Sent" history record — DeliveredCount is set
/// once, at send time, from how many recipients were actually resolved (not a live count; the
/// mockup's history list shows a fixed number per past send, not a recalculated one).</summary>
public sealed class Announcement
{
    private Announcement()
    {
    }

    private Announcement(
        Guid id, string title, string message, AnnouncementAudience audience, bool showAsBanner, bool sendEmail,
        Guid createdByUserId, int deliveredCount, DateTimeOffset now)
    {
        Id = id;
        Title = title;
        Message = message;
        Audience = audience;
        ShowAsBanner = showAsBanner;
        SendEmail = sendEmail;
        CreatedByUserId = createdByUserId;
        DeliveredCount = deliveredCount;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public AnnouncementAudience Audience { get; private set; }

    public bool ShowAsBanner { get; private set; }

    public bool SendEmail { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public int DeliveredCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Announcement Create(
        Guid id, string title, string message, AnnouncementAudience audience, bool showAsBanner, bool sendEmail,
        Guid createdByUserId, int deliveredCount, DateTimeOffset now) =>
        new(id, title, message, audience, showAsBanner, sendEmail, createdByUserId, deliveredCount, now);
}
