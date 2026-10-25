using HRS.Application.Common.Interfaces;
using HRS.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Admin.Webhooks;

/// <summary>Queues an event for every active endpoint that wants it, in the caller's own unit of work
/// (the caller saves) — like the notification outbox, nothing is sent inside the request.</summary>
public static class WebhookEnqueuer
{
    public static async Task EnqueueAsync(
        IAppDbContext db, WebhookEvent webhookEvent, Func<Task<string>> buildPayload, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var endpoints = (await db.WebhookEndpoints.AsNoTracking().Where(e => e.IsActive).ToListAsync(cancellationToken))
            .Where(e => e.Wants(webhookEvent))
            .ToList();
        if (endpoints.Count == 0)
        {
            return;
        }

        var payload = await buildPayload();
        foreach (var endpoint in endpoints)
        {
            db.WebhookDeliveries.Add(WebhookDelivery.Create(Guid.CreateVersion7(), endpoint.Id, webhookEvent, payload, now));
        }
    }
}
