using HRS.Domain.Notifications;

namespace HRS.Application.Features.Notifications.Center;

public sealed record NotificationSummary(
    Guid Id, NotificationEventType EventType, string Subject, string Body, string? Link, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
