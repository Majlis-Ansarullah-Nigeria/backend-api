using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Infrastructure.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ManagementApi.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService? _emailService;
    private readonly INotificationHubService? _signalRHub;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IApplicationDbContext context,
        ILogger<NotificationService> logger,
        IEmailService? emailService = null,
        INotificationHubService? signalRHub = null)
    {
        _context = context;
        _logger = logger;
        _emailService = emailService;
        _signalRHub = signalRHub;
    }

    public async Task SendNotificationAsync(
        NotificationType type,
        Guid recipientId,
        string recipientName,
        string title,
        string message,
        NotificationPriority priority = NotificationPriority.Normal,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new Notification(
                type,
                recipientId,
                recipientName,
                title,
                message,
                priority,
                relatedEntityId,
                relatedEntityType
            );

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Notification sent: Type={Type}, Recipient={RecipientId}, Title={Title}",
                type, recipientId, title);

            // Send real-time notification via SignalR
            if (_signalRHub != null)
            {
                try
                {
                    var notificationDto = NotificationDto.FromEntity(notification);
                    await _signalRHub.SendNotificationToUserAsync(recipientId, notificationDto);
                    _logger.LogInformation("Real-time notification pushed via SignalR to user {UserId}", recipientId);
                }
                catch (Exception signalREx)
                {
                    _logger.LogWarning(signalREx,
                        "Failed to push real-time notification via SignalR for {NotificationId}",
                        notification.Id);
                    // Don't throw - SignalR failure shouldn't fail notification creation
                }
            }

            // Optionally send email for high-priority notifications
            if (_emailService != null && (priority == NotificationPriority.High || priority == NotificationPriority.Urgent))
            {
                try
                {
                    // Get user email from database
                    var user = await _context.Users
                        .Where(u => u.Id == recipientId)
                        .Select(u => new { u.Email, u.UserName })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (user?.Email != null)
                    {
                        await _emailService.SendNotificationEmailAsync(
                            user.Email,
                            user.UserName ?? recipientName,
                            title,
                            message,
                            cancellationToken: cancellationToken);

                        _logger.LogInformation(
                            "Email notification sent to {Email} for notification {NotificationId}",
                            user.Email, notification.Id);
                    }
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx,
                        "Failed to send email notification for {NotificationId}",
                        notification.Id);
                    // Don't throw - email failure shouldn't fail notification creation
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send notification: Type={Type}, Recipient={RecipientId}",
                type, recipientId);
            throw;
        }
    }

    public async Task SendBulkNotificationsAsync(
        NotificationType type,
        List<(Guid UserId, string UserName)> recipients,
        string title,
        string message,
        NotificationPriority priority = NotificationPriority.Normal,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notifications = recipients.Select(r => new Notification(
                type,
                r.UserId,
                r.UserName,
                title,
                message,
                priority,
                relatedEntityId,
                relatedEntityType
            )).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Bulk notifications sent: Type={Type}, Count={Count}",
                type, recipients.Count);

            // Send real-time notifications via SignalR
            if (_signalRHub != null)
            {
                try
                {
                    var notificationDtos = notifications.Select(NotificationDto.FromEntity).ToList();
                    var userIds = recipients.Select(r => r.UserId).ToList();

                    // Send notification to each user individually (could batch if needed)
                    foreach (var (notification, userId) in notifications.Zip(userIds))
                    {
                        var dto = NotificationDto.FromEntity(notification);
                        await _signalRHub.SendNotificationToUserAsync(userId, dto);
                    }

                    _logger.LogInformation("Pushed {Count} real-time notifications via SignalR", recipients.Count);
                }
                catch (Exception signalREx)
                {
                    _logger.LogWarning(signalREx, "Failed to push bulk real-time notifications via SignalR");
                    // Don't throw - SignalR failure shouldn't fail notification creation
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send bulk notifications: Type={Type}, Count={Count}",
                type, recipients.Count);
            throw;
        }
    }

    public async Task NotifyWindowOpenedAsync(
        Guid windowId,
        string windowName,
        DateTime endDate,
        List<(Guid UserId, string UserName)> eligibleUsers,
        CancellationToken cancellationToken = default)
    {
        var title = "New Submission Window Opened";
        var message = $"A new submission window '{windowName}' is now open. Deadline: {endDate:MMMM dd, yyyy}";

        await SendBulkNotificationsAsync(
            NotificationType.WindowOpened,
            eligibleUsers,
            title,
            message,
            NotificationPriority.Normal,
            windowId,
            "SubmissionWindow",
            cancellationToken);
    }

    public async Task NotifySubmissionApprovedAsync(
        Guid submissionId,
        Guid submitterId,
        string submitterName,
        string templateName,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        var title = "Report Submission Approved";
        var message = $"Your submission for '{templateName}' has been approved.";

        if (!string.IsNullOrEmpty(comments))
        {
            message += $" Comments: {comments}";
        }

        await SendNotificationAsync(
            NotificationType.SubmissionApproved,
            submitterId,
            submitterName,
            title,
            message,
            NotificationPriority.Normal,
            submissionId,
            "ReportSubmission",
            cancellationToken);
    }

    public async Task NotifySubmissionRejectedAsync(
        Guid submissionId,
        Guid submitterId,
        string submitterName,
        string templateName,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var title = "Report Submission Rejected";
        var message = $"Your submission for '{templateName}' has been rejected. Reason: {reason}";

        await SendNotificationAsync(
            NotificationType.SubmissionRejected,
            submitterId,
            submitterName,
            title,
            message,
            NotificationPriority.High,
            submissionId,
            "ReportSubmission",
            cancellationToken);
    }

    public async Task NotifyDeadlineApproachingAsync(
        Guid windowId,
        string windowName,
        DateTime endDate,
        int daysRemaining,
        List<(Guid UserId, string UserName)> pendingUsers,
        CancellationToken cancellationToken = default)
    {
        var title = "Submission Deadline Approaching";
        var message = $"The submission window '{windowName}' closes in {daysRemaining} day(s) on {endDate:MMMM dd, yyyy}. Please submit your report.";

        await SendBulkNotificationsAsync(
            NotificationType.DeadlineReminder,
            pendingUsers,
            title,
            message,
            NotificationPriority.High,
            windowId,
            "SubmissionWindow",
            cancellationToken);
    }

    public async Task NotifyBulkApprovedAsync(
        int approvedCount,
        List<(Guid SubmitterId, string SubmitterName)> submitters,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        var title = "Report Submission Approved";
        var message = $"Your report submission has been approved.";

        if (!string.IsNullOrEmpty(comments))
        {
            message += $" Comments: {comments}";
        }

        await SendBulkNotificationsAsync(
            NotificationType.BulkApproved,
            submitters,
            title,
            message,
            NotificationPriority.Normal,
            null,
            null,
            cancellationToken);
    }

    public async Task NotifyBulkRejectedAsync(
        int rejectedCount,
        List<(Guid SubmitterId, string SubmitterName)> submitters,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var title = "Report Submission Rejected";
        var message = $"Your report submission has been rejected. Reason: {reason}";

        await SendBulkNotificationsAsync(
            NotificationType.BulkRejected,
            submitters,
            title,
            message,
            NotificationPriority.High,
            null,
            null,
            cancellationToken);
    }
}
