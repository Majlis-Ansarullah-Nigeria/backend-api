using HRS.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace HRS.Infrastructure.Email;

public sealed partial class BackgroundEmailDispatcher(
    BackgroundEmailChannel queue,
    IOptions<EmailOptions> options,
    ILogger<BackgroundEmailDispatcher> logger) : BackgroundService
{
    private readonly EmailOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await SendAsync(message, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Best-effort for v1: losing an OTP email just means the user requests a new
                // one. Durable, retried delivery arrives with the outbox in M8.1.
                LogSendFailed(ex, message.ToAddress);
            }
        }
    }

    private async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(MailboxAddress.Parse(_options.From));
        mimeMessage.To.Add(MailboxAddress.Parse(message.ToAddress));
        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new TextPart("plain") { Text = message.TextBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.None, cancellationToken);
        await client.SendAsync(mimeMessage, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send email to {ToAddress}")]
    private partial void LogSendFailed(Exception exception, string toAddress);
}
