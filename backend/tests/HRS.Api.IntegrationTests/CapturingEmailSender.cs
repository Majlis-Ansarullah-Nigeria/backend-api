using System.Collections.Concurrent;
using HRS.Application.Common.Interfaces;

namespace HRS.Api.IntegrationTests;

/// <summary>
/// Replaces the real <see cref="IEmailSender"/> in tests so OTP codes can be read back
/// without a live SMTP server, and so tests never depend on network I/O.
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentDictionary<string, EmailMessage> _sentByAddress = new();

    public Task QueueAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sentByAddress[message.ToAddress] = message;
        return Task.CompletedTask;
    }

    public EmailMessage? LastSentTo(string address) => _sentByAddress.GetValueOrDefault(address);
}
