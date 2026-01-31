using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ManagementApi.Infrastructure.Hubs;

/// <summary>
/// SignalR hub for real-time notification delivery
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("User {UserId} connected to notification hub. ConnectionId: {ConnectionId}",
            userId, Context.ConnectionId);

        // Add user to their personal group (userId-based)
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("User {UserId} disconnected from notification hub. ConnectionId: {ConnectionId}",
            userId, Context.ConnectionId);

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client calls this to confirm they received a notification
    /// </summary>
    public async Task AcknowledgeNotification(string notificationId)
    {
        _logger.LogDebug("Notification {NotificationId} acknowledged by user {UserId}",
            notificationId, Context.UserIdentifier);

        // Could track acknowledgment metrics here
        await Task.CompletedTask;
    }
}

/// <summary>
/// Interface for sending notifications via SignalR
/// </summary>
public interface INotificationHubService
{
    /// <summary>
    /// Send notification to a specific user
    /// </summary>
    Task SendNotificationToUserAsync(Guid userId, object notification);

    /// <summary>
    /// Send notification to multiple users
    /// </summary>
    Task SendNotificationToUsersAsync(List<Guid> userIds, object notification);

    /// <summary>
    /// Broadcast notification to all connected users
    /// </summary>
    Task BroadcastNotificationAsync(object notification);
}

/// <summary>
/// Service for sending notifications through SignalR hub
/// </summary>
public class NotificationHubService : INotificationHubService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationHubService> _logger;

    public NotificationHubService(
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationHubService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendNotificationToUserAsync(Guid userId, object notification)
    {
        try
        {
            await _hubContext.Clients
                .Group($"user-{userId}")
                .SendAsync("ReceiveNotification", notification);

            _logger.LogInformation("Sent real-time notification to user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send real-time notification to user {UserId}", userId);
            // Don't throw - SignalR delivery failure shouldn't break the application
        }
    }

    public async Task SendNotificationToUsersAsync(List<Guid> userIds, object notification)
    {
        try
        {
            var groups = userIds.Select(id => $"user-{id}").ToList();

            foreach (var group in groups)
            {
                await _hubContext.Clients
                    .Group(group)
                    .SendAsync("ReceiveNotification", notification);
            }

            _logger.LogInformation("Sent real-time notification to {Count} users", userIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send bulk real-time notifications");
            // Don't throw - SignalR delivery failure shouldn't break the application
        }
    }

    public async Task BroadcastNotificationAsync(object notification)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", notification);
            _logger.LogInformation("Broadcast notification to all connected users");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast notification");
            // Don't throw - SignalR delivery failure shouldn't break the application
        }
    }
}
