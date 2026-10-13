using HRS.Domain.Notifications;

namespace HRS.Application.Features.Notifications.NotificationTemplates;

public sealed record NotificationTemplateSummary(Guid Id, NotificationEventType EventType, string Subject, string Body, DateTimeOffset UpdatedAt)
{
    public static NotificationTemplateSummary FromEntity(NotificationTemplate template) =>
        new(template.Id, template.EventType, template.Subject, template.Body, template.UpdatedAt);
}
