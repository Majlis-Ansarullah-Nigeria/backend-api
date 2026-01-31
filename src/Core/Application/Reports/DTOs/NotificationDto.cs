using ManagementApi.Domain.Entities.Reports;

namespace ManagementApi.Application.Reports.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public string TypeName { get; set; } = default!;
    public Guid RecipientId { get; set; }
    public string RecipientName { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadOn { get; set; }
    public NotificationPriority Priority { get; set; }
    public string PriorityName { get; set; } = default!;
    public DateTime CreatedOn { get; set; }

    public static NotificationDto FromEntity(Notification notification)
    {
        return new NotificationDto
        {
            Id = notification.Id,
            Type = notification.Type,
            TypeName = notification.Type.ToString(),
            RecipientId = notification.RecipientId,
            RecipientName = notification.RecipientName,
            Title = notification.Title,
            Message = notification.Message,
            RelatedEntityId = notification.RelatedEntityId,
            RelatedEntityType = notification.RelatedEntityType,
            IsRead = notification.IsRead,
            ReadOn = notification.ReadOn,
            Priority = notification.Priority,
            PriorityName = notification.Priority.ToString(),
            CreatedOn = notification.CreatedOn
        };
    }
}

public class CreateNotificationRequest
{
    public NotificationType Type { get; set; }
    public Guid RecipientId { get; set; }
    public string RecipientName { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
}

public class NotificationStatsDto
{
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
    public int ReadCount { get; set; }
    public DateTime? LastNotificationDate { get; set; }
}
