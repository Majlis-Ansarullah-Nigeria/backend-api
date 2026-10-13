namespace HRS.Domain.Notifications;

/// <summary>One row per (user, category) the user has ever changed — absence means "both
/// channels enabled" (the Profile mockup's all-on default), so a brand-new user needs no rows at
/// all.</summary>
public sealed class NotificationPreference
{
    private NotificationPreference()
    {
    }

    private NotificationPreference(Guid id, Guid userId, NotificationCategory category, bool inAppEnabled, bool emailEnabled)
    {
        Id = id;
        UserId = userId;
        Category = category;
        InAppEnabled = inAppEnabled;
        EmailEnabled = emailEnabled;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public NotificationCategory Category { get; private set; }

    public bool InAppEnabled { get; private set; } = true;

    public bool EmailEnabled { get; private set; } = true;

    public static NotificationPreference Create(Guid id, Guid userId, NotificationCategory category, bool inAppEnabled, bool emailEnabled) =>
        new(id, userId, category, inAppEnabled, emailEnabled);

    public void Update(bool inAppEnabled, bool emailEnabled)
    {
        InAppEnabled = inAppEnabled;
        EmailEnabled = emailEnabled;
    }
}
