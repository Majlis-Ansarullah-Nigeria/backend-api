using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Notifications;
using HRS.Application.Features.Notifications.Center;
using HRS.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

/// <summary>FR-NOT-001's notification centre and web push subscriptions. Every assertion is
/// scoped to this test's own freshly-created user — the same shared, never-reset test database
/// reasoning as OutboxDispatcherTests applies here too.</summary>
[Collection(ApiTestCollection.Name)]
public class NotificationCentreEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private async Task<(HttpClient Client, Guid UserId)> CreateLoggedInClientAsync()
    {
        var email = UniqueEmail();
        var userId = await factory.CreateUserAsync(email, "Correct-Horse-Notif1");

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-Notif1"));

        return (client, userId);
    }

    private async Task<Guid> SeedNotificationAsync(Guid userId, NotificationEventType eventType, string subject, DateTimeOffset createdAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var outbox = OutboxMessage.Create(Guid.CreateVersion7(), eventType, userId, "{}", createdAt);
        db.OutboxMessages.Add(outbox);

        var notification = Notification.Create(Guid.CreateVersion7(), userId, eventType, subject, "Body text.", "http://x", outbox.Id, createdAt);
        db.Notifications.Add(notification);

        await db.SaveChangesAsync();
        return notification.Id;
    }

    [Fact]
    public async Task ListReturnsNewestFirst()
    {
        var (client, userId) = await CreateLoggedInClientAsync();
        var now = DateTimeOffset.UtcNow;
        await SeedNotificationAsync(userId, NotificationEventType.WindowOpened, "Older", now.AddHours(-2));
        await SeedNotificationAsync(userId, NotificationEventType.Overdue, "Newer", now);

        var response = await client.GetAsync(new Uri("/api/v1/me/notifications?filter=All", UriKind.Relative));
        var list = await response.Content.ReadFromJsonAsync<List<NotificationSummary>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Newer", list![0].Subject);
        Assert.Equal("Older", list[1].Subject);
    }

    [Fact]
    public async Task UnreadFilterExcludesReadNotifications()
    {
        var (client, userId) = await CreateLoggedInClientAsync();
        var now = DateTimeOffset.UtcNow;
        var readId = await SeedNotificationAsync(userId, NotificationEventType.WindowOpened, "Already read", now);
        await SeedNotificationAsync(userId, NotificationEventType.Overdue, "Still unread", now);

        await client.PostAsync(new Uri($"/api/v1/me/notifications/{readId}/read", UriKind.Relative), content: null);

        var response = await client.GetAsync(new Uri("/api/v1/me/notifications?filter=Unread", UriKind.Relative));
        var list = await response.Content.ReadFromJsonAsync<List<NotificationSummary>>();

        var only = Assert.Single(list!);
        Assert.Equal("Still unread", only.Subject);
    }

    [Fact]
    public async Task DeadlinesFilterIncludesOnlyDueDateEvents()
    {
        var (client, userId) = await CreateLoggedInClientAsync();
        var now = DateTimeOffset.UtcNow;
        await SeedNotificationAsync(userId, NotificationEventType.Overdue, "A deadline event", now);
        await SeedNotificationAsync(userId, NotificationEventType.CommentAdded, "Not a deadline event", now);

        var response = await client.GetAsync(new Uri("/api/v1/me/notifications?filter=Deadlines", UriKind.Relative));
        var list = await response.Content.ReadFromJsonAsync<List<NotificationSummary>>();

        var only = Assert.Single(list!);
        Assert.Equal("A deadline event", only.Subject);
    }

    [Fact]
    public async Task UnreadCountReflectsOnlyThisUsersUnreadRows()
    {
        var (client, userId) = await CreateLoggedInClientAsync();
        var now = DateTimeOffset.UtcNow;
        var readId = await SeedNotificationAsync(userId, NotificationEventType.WindowOpened, "Read one", now);
        await SeedNotificationAsync(userId, NotificationEventType.Overdue, "Unread one", now);
        await SeedNotificationAsync(userId, NotificationEventType.Reminder, "Unread two", now);

        var before = await client.GetFromJsonAsync<int>("/api/v1/me/notifications/unread-count");
        Assert.Equal(3, before);

        await client.PostAsync(new Uri($"/api/v1/me/notifications/{readId}/read", UriKind.Relative), content: null);

        var after = await client.GetFromJsonAsync<int>("/api/v1/me/notifications/unread-count");
        Assert.Equal(2, after);
    }

    [Fact]
    public async Task MarkReadIsIdempotent()
    {
        var (client, userId) = await CreateLoggedInClientAsync();
        var id = await SeedNotificationAsync(userId, NotificationEventType.WindowOpened, "Subject", DateTimeOffset.UtcNow);

        var first = await client.PostAsync(new Uri($"/api/v1/me/notifications/{id}/read", UriKind.Relative), content: null);
        var second = await client.PostAsync(new Uri($"/api/v1/me/notifications/{id}/read", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task MarkReadOnAnotherUsersNotificationReturnsNotFound()
    {
        var (client, _) = await CreateLoggedInClientAsync();
        var otherUserId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Notif2");
        var otherUsersNotification = await SeedNotificationAsync(otherUserId, NotificationEventType.WindowOpened, "Not yours", DateTimeOffset.UtcNow);

        var response = await client.PostAsync(new Uri($"/api/v1/me/notifications/{otherUsersNotification}/read", UriKind.Relative), content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MarkAllReadClearsEveryUnreadRowForThisUserOnly()
    {
        var (client, userId) = await CreateLoggedInClientAsync();
        var now = DateTimeOffset.UtcNow;
        await SeedNotificationAsync(userId, NotificationEventType.WindowOpened, "One", now);
        await SeedNotificationAsync(userId, NotificationEventType.Overdue, "Two", now);

        var response = await client.PostAsync(new Uri("/api/v1/me/notifications/read-all", UriKind.Relative), content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var afterCount = await client.GetFromJsonAsync<int>("/api/v1/me/notifications/unread-count");
        Assert.Equal(0, afterCount);
    }

    [Fact]
    public async Task NotificationEndpointsRequireAuthentication()
    {
        using var client = factory.CreateAuthenticatedClient();

        var list = await client.GetAsync(new Uri("/api/v1/me/notifications?filter=All", UriKind.Relative));
        var unreadCount = await client.GetAsync(new Uri("/api/v1/me/notifications/unread-count", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unreadCount.StatusCode);
    }

    [Fact]
    public async Task SavingAPushSubscriptionTwiceWithTheSameEndpointUpsertsRatherThanDuplicating()
    {
        var (client, _) = await CreateLoggedInClientAsync();
        var endpoint = $"https://push.example.org/{Guid.NewGuid():N}";

        var first = await client.PostAsJsonAsync("/api/v1/me/push-subscriptions", new SavePushSubscriptionRequest(endpoint, "p256dh-1", "auth-1"));
        var second = await client.PostAsJsonAsync("/api/v1/me/push-subscriptions", new SavePushSubscriptionRequest(endpoint, "p256dh-2", "auth-2"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var rows = await db.PushSubscriptions.Where(s => s.Endpoint == endpoint).ToListAsync();
        var only = Assert.Single(rows);
        Assert.Equal("p256dh-2", only.P256Dh);
    }

    [Fact]
    public async Task DeletingAPushSubscriptionRemovesIt()
    {
        var (client, _) = await CreateLoggedInClientAsync();
        var endpoint = $"https://push.example.org/{Guid.NewGuid():N}";
        await client.PostAsJsonAsync("/api/v1/me/push-subscriptions", new SavePushSubscriptionRequest(endpoint, "p256dh", "auth"));

        var response = await client.DeleteAsync(new Uri($"/api/v1/me/push-subscriptions?endpoint={Uri.EscapeDataString(endpoint)}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        Assert.False(await db.PushSubscriptions.AnyAsync(s => s.Endpoint == endpoint));
    }

    [Fact]
    public async Task VapidPublicKeyEndpointReturnsTheConfiguredKey()
    {
        var (client, _) = await CreateLoggedInClientAsync();

        var response = await client.GetAsync(new Uri("/api/v1/me/push-subscriptions/vapid-public-key", UriKind.Relative));
        var key = await response.Content.ReadFromJsonAsync<string>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(key));
    }

    [Fact]
    public async Task TheDispatcherCreatesExactlyOneInAppNotificationPerMessageEvenAcrossRetries()
    {
        var (_, userId) = await CreateLoggedInClientAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        OutboxWriter.EnqueueForUser(db, userId, NotificationEventType.ReportAcknowledged, new Dictionary<string, string>
        {
            ["unitName"] = "Dila Ibafo",
            ["reportTypeName"] = "Monthly Report",
            ["periodLabel"] = "Sep 2026",
            ["link"] = "http://x",
        }, clock.UtcNow);
        await db.SaveChangesAsync();

        for (var i = 0; i < 3; i++)
        {
            await OutboxDispatcher.RunAsync(db, identityService, factory.EmailSender, factory.PushSender, factory.Broadcaster, clock, CancellationToken.None);
        }

        var notifications = await db.Notifications.AsNoTracking().Where(n => n.UserId == userId).ToListAsync();
        Assert.Single(notifications);
        Assert.Equal(1, factory.Broadcaster.LastCountFor(userId));
    }

    [Fact]
    public async Task TheDispatcherAttemptsPushToEveryActiveSubscriptionAndDeletesAGoneOne()
    {
        var (client, userId) = await CreateLoggedInClientAsync();
        var liveEndpoint = $"https://push.example.org/{Guid.NewGuid():N}";
        var goneEndpoint = $"https://push.example.org/{Guid.NewGuid():N}";
        await client.PostAsJsonAsync("/api/v1/me/push-subscriptions", new SavePushSubscriptionRequest(liveEndpoint, "p256dh", "auth"));
        await client.PostAsJsonAsync("/api/v1/me/push-subscriptions", new SavePushSubscriptionRequest(goneEndpoint, "p256dh", "auth"));
        factory.PushSender.GoneEndpoints.Add(goneEndpoint);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        OutboxWriter.EnqueueForUser(db, userId, NotificationEventType.ReportAcknowledged, new Dictionary<string, string>
        {
            ["unitName"] = "Dila Ibafo",
            ["reportTypeName"] = "Monthly Report",
            ["periodLabel"] = "Sep 2026",
            ["link"] = "http://x",
        }, clock.UtcNow);
        await db.SaveChangesAsync();

        for (var i = 0; i < 3; i++)
        {
            await OutboxDispatcher.RunAsync(db, identityService, factory.EmailSender, factory.PushSender, factory.Broadcaster, clock, CancellationToken.None);
        }

        Assert.NotNull(factory.PushSender.LastSentTo(liveEndpoint));
        var remaining = await db.PushSubscriptions.AsNoTracking().Where(s => s.Endpoint == liveEndpoint || s.Endpoint == goneEndpoint).ToListAsync();
        var only = Assert.Single(remaining);
        Assert.Equal(liveEndpoint, only.Endpoint);
    }
}
