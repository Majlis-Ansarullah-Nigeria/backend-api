namespace HRS.Application.Common.Interfaces;

/// <summary>
/// Queues an email for background delivery. Handlers must never await the actual send on the
/// request thread (backend/CLAUDE.md: "Never send email inside a request") — implementations
/// hand the message to a background dispatcher and return immediately.
/// </summary>
public interface IEmailSender
{
    Task QueueAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed record EmailMessage(string ToAddress, string Subject, string TextBody);
