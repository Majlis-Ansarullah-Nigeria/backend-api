using HRS.Application.Common.Interfaces;
using HRS.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Admin.Webhooks;

/// <summary>FR-ADM-003's sender, run by the worker every minute: due deliveries are signed and POSTed;
/// failures back off (WebhookDelivery.Backoff) and give up after the last attempt.</summary>
public static class WebhookDispatcher
{
    private const int BatchSize = 50;

    public static async Task<int> RunAsync(IAppDbContext db, IWebhookSender sender, IClock clock, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var due = await db.WebhookDeliveries
            .Where(d => d.Status == WebhookDeliveryStatus.Pending && d.NextAttemptAt <= now)
            .OrderBy(d => d.NextAttemptAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
        if (due.Count == 0)
        {
            return 0;
        }

        var endpointIds = due.Select(d => d.EndpointId).Distinct().ToList();
        var endpoints = await db.WebhookEndpoints.AsNoTracking().Where(e => endpointIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, cancellationToken);

        var delivered = 0;
        foreach (var delivery in due)
        {
            if (!endpoints.TryGetValue(delivery.EndpointId, out var endpoint) || !endpoint.IsActive)
            {
                delivery.Abandon("The endpoint was removed or switched off.");
                continue;
            }

            var timestamp = now.ToUnixTimeSeconds();
            var request = new WebhookRequest(
                endpoint.Url, WebhookEventNames.Name(delivery.Event), delivery.Id, timestamp,
                WebhookSigning.Sign(endpoint.Secret, timestamp, delivery.PayloadJson), delivery.PayloadJson);

            WebhookSendResult result;
            try
            {
                result = await sender.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                result = new WebhookSendResult(false, null, ex.Message);
            }

            if (result.Success)
            {
                delivery.RecordSuccess(result.StatusCode ?? 200, clock.UtcNow);
                delivered++;
            }
            else
            {
                delivery.RecordFailure(result.StatusCode, result.Error ?? "Delivery failed.", clock.UtcNow);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return delivered;
    }
}
