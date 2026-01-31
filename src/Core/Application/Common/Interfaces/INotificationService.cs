using ManagementApi.Domain.Entities.Reports;

namespace ManagementApi.Application.Common.Interfaces;

/// <summary>
/// Service for creating and sending notifications to users
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Send a notification to a single user
    /// </summary>
    Task SendNotificationAsync(
        NotificationType type,
        Guid recipientId,
        string recipientName,
        string title,
        string message,
        NotificationPriority priority = NotificationPriority.Normal,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a notification to multiple users
    /// </summary>
    Task SendBulkNotificationsAsync(
        NotificationType type,
        List<(Guid UserId, string UserName)> recipients,
        string title,
        string message,
        NotificationPriority priority = NotificationPriority.Normal,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify when a submission window opens
    /// </summary>
    Task NotifyWindowOpenedAsync(
        Guid windowId,
        string windowName,
        DateTime endDate,
        List<(Guid UserId, string UserName)> eligibleUsers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify when a submission is approved
    /// </summary>
    Task NotifySubmissionApprovedAsync(
        Guid submissionId,
        Guid submitterId,
        string submitterName,
        string templateName,
        string? comments = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify when a submission is rejected
    /// </summary>
    Task NotifySubmissionRejectedAsync(
        Guid submissionId,
        Guid submitterId,
        string submitterName,
        string templateName,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify about approaching deadline
    /// </summary>
    Task NotifyDeadlineApproachingAsync(
        Guid windowId,
        string windowName,
        DateTime endDate,
        int daysRemaining,
        List<(Guid UserId, string UserName)> pendingUsers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify about bulk approval
    /// </summary>
    Task NotifyBulkApprovedAsync(
        int approvedCount,
        List<(Guid SubmitterId, string SubmitterName)> submitters,
        string? comments = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify about bulk rejection
    /// </summary>
    Task NotifyBulkRejectedAsync(
        int rejectedCount,
        List<(Guid SubmitterId, string SubmitterName)> submitters,
        string reason,
        CancellationToken cancellationToken = default);
}
