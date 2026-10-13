using HRS.Domain.Notifications;

namespace HRS.Application.Features.Notifications.Preferences;

public sealed record NotificationPreferenceView(NotificationCategory Category, bool InAppEnabled, bool EmailEnabled);
