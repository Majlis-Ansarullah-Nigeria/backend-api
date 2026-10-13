using System.Collections.Concurrent;
using HRS.Application.Common.Interfaces;

namespace HRS.Api.IntegrationTests;

/// <summary>
/// Replaces the real <see cref="INotificationBroadcaster"/> (SignalR over a Redis backplane) in
/// tests — no live hub connection to assert against, so tests only check that the dispatcher
/// attempted a broadcast with the right count, keyed by user so the shared test database's other
/// tests can't contaminate the assertion.
/// </summary>
public sealed class CapturingNotificationBroadcaster : INotificationBroadcaster
{
    private readonly ConcurrentDictionary<Guid, int> _lastCountByUser = new();

    public Task NotifyUnreadCountChangedAsync(Guid userId, int unreadCount, CancellationToken cancellationToken)
    {
        _lastCountByUser[userId] = unreadCount;
        return Task.CompletedTask;
    }

    public int? LastCountFor(Guid userId) => _lastCountByUser.GetValueOrDefault(userId);
}
