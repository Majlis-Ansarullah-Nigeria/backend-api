namespace HRS.Domain.Webhooks;

public enum WebhookDeliveryStatus
{
    Pending,
    Delivered,
    Failed,
}

/// <summary>
/// One attempt-tracked delivery of one event to one endpoint (FR-ADM-003), written in the same
/// transaction as the change it reports and sent by the worker. The payload is a snapshot taken at
/// that moment, so a retry sends exactly what was first queued. Retries back off over about half a
/// day before giving up.
/// </summary>
public sealed class WebhookDelivery
{
    /// <summary>Waits before attempts 2…6; after the last, the delivery fails.</summary>
    public static readonly TimeSpan[] Backoff =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(12)];

    private WebhookDelivery()
    {
    }

    public Guid Id { get; private set; }

    public Guid EndpointId { get; private set; }

    public WebhookEvent Event { get; private set; }

    public string PayloadJson { get; private set; } = "{}";

    public WebhookDeliveryStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public int? LastStatusCode { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public static WebhookDelivery Create(Guid id, Guid endpointId, WebhookEvent webhookEvent, string payloadJson, DateTimeOffset now) =>
        new()
        {
            Id = id,
            EndpointId = endpointId,
            Event = webhookEvent,
            PayloadJson = payloadJson,
            Status = WebhookDeliveryStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now,
        };

    public void RecordSuccess(int statusCode, DateTimeOffset now)
    {
        Attempts++;
        Status = WebhookDeliveryStatus.Delivered;
        LastStatusCode = statusCode;
        LastError = null;
        DeliveredAt = now;
    }

    public void RecordFailure(int? statusCode, string error, DateTimeOffset now)
    {
        Attempts++;
        LastStatusCode = statusCode;
        LastError = error.Length > 500 ? error[..500] : error;
        if (Attempts > Backoff.Length)
        {
            Status = WebhookDeliveryStatus.Failed;
            return;
        }

        NextAttemptAt = now + Backoff[Attempts - 1];
    }

    /// <summary>An endpoint removed or switched off after the delivery was queued: stop trying.</summary>
    public void Abandon(string reason)
    {
        Status = WebhookDeliveryStatus.Failed;
        LastError = reason;
    }
}
