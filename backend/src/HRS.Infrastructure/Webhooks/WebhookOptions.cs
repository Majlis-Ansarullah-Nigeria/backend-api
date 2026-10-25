namespace HRS.Infrastructure.Webhooks;

public sealed class WebhookOptions
{
    /// <summary>Lets webhooks reach private addresses — for local development only; off by default.</summary>
    public bool AllowPrivateNetworks { get; set; }
}
