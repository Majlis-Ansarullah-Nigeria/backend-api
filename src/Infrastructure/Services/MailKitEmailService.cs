using ManagementApi.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using System.Net.Mail;

namespace ManagementApi.Infrastructure.Services;

/// <summary>
/// Email service implementation using MailKit
/// </summary>
public class MailKitEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly IEmailTemplateService _templateService;
    private readonly ILogger<MailKitEmailService> _logger;

    public MailKitEmailService(
        IConfiguration configuration,
        IEmailTemplateService templateService,
        ILogger<MailKitEmailService> logger)
    {
        _configuration = configuration;
        _templateService = templateService;
        _logger = logger;
    }

    public async Task SendEmailAsync(
        string toEmail,
        string toName,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new MimeMessage();

            // From
            var fromEmail = _configuration["EmailSettings:FromEmail"] ?? "noreply@ansarullah.ng";
            var fromName = _configuration["EmailSettings:FromName"] ?? "Majlis Ansarullah Nigeria";
            message.From.Add(new MailboxAddress(fromName, fromEmail));

            // To
            message.To.Add(new MailboxAddress(toName, toEmail));

            // Subject
            message.Subject = subject;

            // Body
            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody,
                TextBody = textBody ?? StripHtml(htmlBody)
            };

            message.Body = bodyBuilder.ToMessageBody();

            // Send
            await SendEmailMessageAsync(message, cancellationToken);

            _logger.LogInformation("Email sent successfully to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            throw;
        }
    }

    public async Task SendBulkEmailAsync(
        List<(string Email, string Name)> recipients,
        string subject,
        string htmlBody,
        string? textBody = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = recipients.Select(recipient =>
            SendEmailAsync(recipient.Email, recipient.Name, subject, htmlBody, textBody, cancellationToken)
        );

        await Task.WhenAll(tasks);

        _logger.LogInformation("Bulk email sent to {Count} recipients", recipients.Count);
    }

    public async Task SendNotificationEmailAsync(
        string toEmail,
        string toName,
        string notificationTitle,
        string notificationMessage,
        string? actionUrl = null,
        string? actionText = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate HTML template
            var htmlBody = _templateService.GenerateNotificationEmail(
                toName,
                notificationTitle,
                notificationMessage,
                Domain.Entities.Reports.NotificationPriority.Normal,
                actionUrl,
                actionText
            );

            await SendEmailAsync(
                toEmail,
                toName,
                notificationTitle,
                htmlBody,
                notificationMessage,
                cancellationToken
            );

            _logger.LogInformation("Notification email sent to {Email}: {Title}", toEmail, notificationTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification email to {Email}", toEmail);
            throw;
        }
    }

    private async Task SendEmailMessageAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        var smtpHost = _configuration["EmailSettings:SmtpHost"];
        var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
        var smtpUser = _configuration["EmailSettings:SmtpUser"];
        var smtpPassword = _configuration["EmailSettings:SmtpPassword"];
        var useSsl = bool.Parse(_configuration["EmailSettings:UseSsl"] ?? "true");

        if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUser))
        {
            _logger.LogWarning("Email settings not configured. Skipping email send.");
            return;
        }

        using var client = new MailKit.Net.Smtp.SmtpClient();

        try
        {
            // Connect
            await client.ConnectAsync(smtpHost, smtpPort,
                useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                cancellationToken);

            // Authenticate
            await client.AuthenticateAsync(smtpUser, smtpPassword, cancellationToken);

            // Send
            await client.SendAsync(message, cancellationToken);

            // Disconnect
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogDebug("SMTP email sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP error while sending email");
            throw;
        }
    }

    private static string StripHtml(string html)
    {
        // Simple HTML stripping for plain text fallback
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }
}
