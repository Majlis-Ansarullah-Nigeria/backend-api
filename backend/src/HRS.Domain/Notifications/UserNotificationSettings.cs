namespace HRS.Domain.Notifications;

/// <summary>FR-NOT-004's "daily digest instead of single emails" toggle, one row per user who has
/// ever changed it (absence = digest off, matching the mockup's default). LastDigestSentAt is the
/// cursor DigestRecurringJob uses to pick up only notifications created since the last run.</summary>
public sealed class UserNotificationSettings
{
    private UserNotificationSettings()
    {
    }

    private UserNotificationSettings(Guid userId, bool digestEnabled)
    {
        UserId = userId;
        DigestEnabled = digestEnabled;
    }

    public Guid UserId { get; private set; }

    public bool DigestEnabled { get; private set; }

    public DateTimeOffset? LastDigestSentAt { get; private set; }

    public static UserNotificationSettings Create(Guid userId, bool digestEnabled) => new(userId, digestEnabled);

    public void SetDigestEnabled(bool digestEnabled) => DigestEnabled = digestEnabled;

    public void MarkDigestSent(DateTimeOffset now) => LastDigestSentAt = now;
}
