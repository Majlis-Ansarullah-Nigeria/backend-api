using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Admin.Webhooks;

namespace HRS.Worker;

/// <summary>Hangfire's resolvable unit of work for FR-ADM-003's webhook deliveries — a thin adapter
/// over <see cref="WebhookDispatcher"/>, the same shape as <see cref="OutboxDispatcherRecurringJob"/>.</summary>
public sealed partial class WebhookDispatcherRecurringJob(
    IAppDbContext db, IWebhookSender sender, IClock clock, ILogger<WebhookDispatcherRecurringJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var delivered = await WebhookDispatcher.RunAsync(db, sender, clock, cancellationToken);
        if (delivered > 0)
        {
            LogDelivered(delivered);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook dispatcher delivered {Count} event(s).")]
    private partial void LogDelivered(int count);
}
