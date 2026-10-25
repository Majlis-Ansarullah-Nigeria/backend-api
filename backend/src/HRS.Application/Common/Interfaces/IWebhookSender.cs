namespace HRS.Application.Common.Interfaces;

public sealed record WebhookSendResult(bool Success, int? StatusCode, string? Error);

/// <summary>POSTs one signed webhook delivery (FR-ADM-003). Implementations must refuse to connect
/// to private, loopback or link-local addresses so a webhook can't reach inside the network.</summary>
public interface IWebhookSender
{
    Task<WebhookSendResult> SendAsync(WebhookRequest request, CancellationToken cancellationToken);
}

public sealed record WebhookRequest(string Url, string EventName, Guid DeliveryId, long Timestamp, string Signature, string Body);
