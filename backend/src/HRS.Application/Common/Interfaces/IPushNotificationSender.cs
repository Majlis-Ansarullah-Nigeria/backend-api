using HRS.Domain.Notifications;

namespace HRS.Application.Common.Interfaces;

/// <summary>
/// Sends a single Web Push message (FR-NOT-001's "web push for installed PWAs") to one
/// subscription. Best-effort by design: the dispatcher never lets a push failure affect an
/// outbox message's own email retry state — only <see cref="PushSubscriptionGoneException"/>
/// has any effect at all (it tells the caller the subscription itself is dead).
/// </summary>
public interface IPushNotificationSender
{
    Task SendAsync(PushSubscription subscription, PushPayload payload, CancellationToken cancellationToken);
}

public sealed record PushPayload(string Title, string Body, string? Link);

/// <summary>The push service told us this subscription is gone (HTTP 404/410) — the browser
/// dropped it, so the caller should delete the row rather than keep retrying it forever.</summary>
public sealed class PushSubscriptionGoneException : Exception
{
    public PushSubscriptionGoneException()
        : base("The push subscription is no longer valid.")
    {
    }
}
