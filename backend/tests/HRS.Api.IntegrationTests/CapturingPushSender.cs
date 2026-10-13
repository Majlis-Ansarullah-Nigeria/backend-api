using System.Collections.Concurrent;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Notifications;

namespace HRS.Api.IntegrationTests;

/// <summary>
/// Replaces the real <see cref="IPushNotificationSender"/> in tests — no real Web Push service,
/// no network I/O. Keyed by subscription endpoint so a test can assert what a specific
/// subscription received without raw-counting across the whole shared test database.
/// </summary>
public sealed class CapturingPushSender : IPushNotificationSender
{
    private readonly ConcurrentDictionary<string, PushPayload> _sentByEndpoint = new();

    /// <summary>Set by a test to make the next send(s) to this endpoint fail as "gone" (404/410),
    /// exercising the dispatcher's delete-the-subscription path.</summary>
    public HashSet<string> GoneEndpoints { get; } = [];

    public Task SendAsync(PushSubscription subscription, PushPayload payload, CancellationToken cancellationToken)
    {
        if (GoneEndpoints.Contains(subscription.Endpoint))
        {
            throw new PushSubscriptionGoneException();
        }

        _sentByEndpoint[subscription.Endpoint] = payload;
        return Task.CompletedTask;
    }

    public PushPayload? LastSentTo(string endpoint) => _sentByEndpoint.GetValueOrDefault(endpoint);
}
