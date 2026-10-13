namespace HRS.Application.Common.Interfaces;

/// <summary>
/// Pushes a live "your unread count changed" signal to a user's open browser tabs, so the
/// notification badge updates without a refresh. Deliberately sends just the count, not the
/// notification payload itself — the client re-fetches the list/count through the normal API,
/// keeping the realtime channel a thin nudge rather than a second source of truth.
/// </summary>
public interface INotificationBroadcaster
{
    Task NotifyUnreadCountChangedAsync(Guid userId, int unreadCount, CancellationToken cancellationToken);
}
