using HRS.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace HRS.Infrastructure.Notifications;

public sealed class SignalRNotificationBroadcaster(IHubContext<NotificationsHub> hub) : INotificationBroadcaster
{
    public Task NotifyUnreadCountChangedAsync(Guid userId, int unreadCount, CancellationToken cancellationToken) =>
        hub.Clients.User(userId.ToString()).SendAsync("unreadCountChanged", unreadCount, cancellationToken);
}
