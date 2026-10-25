namespace HRS.Domain.Webhooks;

/// <summary>FR-ADM-003's webhook events. Ping is the test delivery sent from the admin page.</summary>
public enum WebhookEvent
{
    ReportSubmitted,
    ReportAcknowledged,
    Ping,
}

public static class WebhookEventNames
{
    /// <summary>The event's name on the wire (the X-HRS-Event header and the payload's "event").</summary>
    public static string Name(WebhookEvent webhookEvent) =>
        webhookEvent switch
        {
            WebhookEvent.ReportSubmitted => "report.submitted",
            WebhookEvent.ReportAcknowledged => "report.acknowledged",
            _ => "ping",
        };
}
