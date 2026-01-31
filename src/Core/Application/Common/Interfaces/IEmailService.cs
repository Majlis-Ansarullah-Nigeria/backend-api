namespace ManagementApi.Application.Common.Interfaces;

/// <summary>
/// Service for sending emails
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send an email to a single recipient
    /// </summary>
    Task SendEmailAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send an email to multiple recipients
    /// </summary>
    Task SendBulkEmailAsync(
        List<(string Email, string Name)> recipients,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send notification email
    /// </summary>
    Task SendNotificationEmailAsync(
        string toEmail,
        string toName,
        string notificationTitle,
        string notificationMessage,
        string? actionUrl = null,
        string? actionText = null,
        CancellationToken cancellationToken = default);
}
