using Microsoft.AspNetCore.SignalR;

namespace HRS.Infrastructure.Notifications;

/// <summary>
/// FR-NOT-001's live badge channel — server-to-client only, so there are no callable hub methods.
/// Lives in Infrastructure rather than HRS.Api (where backend/CLAUDE.md's dependency table
/// otherwise places "SignalR hub") because HRS.Worker, which never references HRS.Api, is the
/// process that actually pushes through it (via <see cref="IHubContext{THub}"/>) whenever the
/// outbox dispatcher creates a new in-app notification. HRS.Api still owns the one thing that
/// *is* a web concern — mapping the "/hubs/notifications" endpoint itself (architecture.md routes
/// it there) — so both processes share one thin, logic-free class instead of each re-registering
/// SignalR's own "virtual" hub discovery mechanism separately.
/// </summary>
public sealed class NotificationsHub : Hub
{
}

/// <summary>Resolves <c>Clients.User(id)</c> from the JWT "sub" claim — this app's auth issues
/// that, not the default <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/> SignalR
/// expects out of the box (see Api's ClaimsPrincipalExtensions.GetUserId()).</summary>
public sealed class JwtSubUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User.FindFirst(Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Sub)?.Value;
}
