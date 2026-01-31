using ManagementApi.Domain.Common;

namespace ManagementApi.Domain.Entities.Reports;

/// <summary>
/// Represents user preferences for notification delivery
/// </summary>
public class NotificationPreference : AuditableEntity
{
    public Guid UserId { get; private set; }
    public NotificationType NotificationType { get; private set; }
    public bool IsInAppEnabled { get; private set; }
    public bool IsEmailEnabled { get; private set; }
    public bool IsPushEnabled { get; private set; }

    // For EF Core
    private NotificationPreference() { }

    public NotificationPreference(
        Guid userId,
        NotificationType notificationType,
        bool isInAppEnabled = true,
        bool isEmailEnabled = false,
        bool isPushEnabled = false)
    {
        UserId = userId;
        NotificationType = notificationType;
        IsInAppEnabled = isInAppEnabled;
        IsEmailEnabled = isEmailEnabled;
        IsPushEnabled = isPushEnabled;
    }

    public void UpdatePreference(bool isInAppEnabled, bool isEmailEnabled, bool isPushEnabled)
    {
        IsInAppEnabled = isInAppEnabled;
        IsEmailEnabled = isEmailEnabled;
        IsPushEnabled = isPushEnabled;
    }

    public void EnableInApp() => IsInAppEnabled = true;
    public void DisableInApp() => IsInAppEnabled = false;

    public void EnableEmail() => IsEmailEnabled = true;
    public void DisableEmail() => IsEmailEnabled = false;

    public void EnablePush() => IsPushEnabled = true;
    public void DisablePush() => IsPushEnabled = false;
}
