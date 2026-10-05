using System.Threading.Channels;
using HRS.Application.Common.Interfaces;

namespace HRS.Infrastructure.Email;

/// <summary>
/// In-memory hand-off so handlers never await an SMTP round-trip on the request thread.
/// Not durable across a restart — acceptable for short-lived OTP codes; a persisted,
/// retried outbox lands with the full notification system in M8.1.
/// </summary>
public sealed class BackgroundEmailChannel : IEmailSender
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(1_000) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    public Task QueueAsync(EmailMessage message, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(message, cancellationToken).AsTask();

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
