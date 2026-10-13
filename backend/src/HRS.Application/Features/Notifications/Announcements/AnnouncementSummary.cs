using HRS.Domain.Notifications;

namespace HRS.Application.Features.Notifications.Announcements;

public sealed record AnnouncementSummary(
    Guid Id, string Title, string Message, AnnouncementAudience Audience, bool ShowAsBanner, bool SendEmail,
    int DeliveredCount, DateTimeOffset CreatedAt);
