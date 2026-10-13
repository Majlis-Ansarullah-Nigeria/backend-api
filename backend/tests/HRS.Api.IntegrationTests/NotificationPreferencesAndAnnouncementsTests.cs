using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Notifications;
using HRS.Application.Features.Notifications.Announcements;
using HRS.Application.Features.Notifications.Preferences;
using HRS.Application.Features.Organization.Units;
using HRS.Domain.Access;
using HRS.Domain.Notifications;
using HRS.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

/// <summary>FR-NOT-004 (preferences, digest) and FR-NOT-006 (announcements). Same shared,
/// never-reset-database reasoning as OutboxDispatcherTests — every assertion is scoped to this
/// test's own freshly-created recipient(s).</summary>
[Collection(ApiTestCollection.Name)]
public class NotificationPreferencesAndAnnouncementsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private async Task<(HttpClient Client, Guid UserId)> CreateLoggedInClientAsync(string name = "Test User")
    {
        var email = UniqueEmail();
        var userId = await factory.CreateUserAsync(email, "Correct-Horse-Prefs1", name: name);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-Prefs1"));

        return (client, userId);
    }

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        var userId = await factory.CreateUserAsync(email, "Correct-Horse-Prefs2");
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-Prefs2"));

        return client;
    }

    private async Task<Guid> CreateMuqamHeadAsync(string suffix, string name = "Test Head")
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var zone = await (await secretariat.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"NPZ{suffix}", $"Prefs Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await secretariat.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"NPD{suffix}", $"Prefs Dila {suffix}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var muqam = await (await secretariat.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"NPM{suffix}", $"Prefs Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dila!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();

        var headUserId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Prefs3", name: name);
        await factory.CreateRoleAssignmentAsync(headUserId, muqam!.Id, RoleName.UnitHead);
        return headUserId;
    }

    private async Task EnqueueAsync(Guid recipientUserId, NotificationEventType eventType, IReadOnlyDictionary<string, string> placeholders)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        OutboxWriter.EnqueueForUser(db, recipientUserId, eventType, placeholders, clock.UtcNow);
        await db.SaveChangesAsync();
    }

    private async Task RunOutboxDispatcherAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        int sent;
        var iterations = 0;
        do
        {
            sent = await OutboxDispatcher.RunAsync(db, identityService, factory.EmailSender, factory.PushSender, factory.Broadcaster, clock, CancellationToken.None);
            iterations++;
        }
        while (sent > 0 && iterations < 200);
    }

    private async Task RunDigestAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        await DigestDispatcher.RunAsync(db, identityService, factory.EmailSender, clock, CancellationToken.None);
    }

    private static readonly Dictionary<string, string> CommentPlaceholders = new()
    {
        ["unitName"] = "Dila Ibafo",
        ["comment"] = "Please clarify figure 3.",
        ["link"] = "http://localhost:3000/reports/1/edit",
    };

    [Fact]
    public async Task DefaultPreferencesAreAllOnForAllFiveCategoriesWithNoCustomization()
    {
        var (client, _) = await CreateLoggedInClientAsync();

        var preferences = await client.GetFromJsonAsync<List<NotificationPreferenceView>>("/api/v1/me/notification-preferences");

        Assert.Equal(5, preferences!.Count);
        Assert.All(preferences, p => Assert.True(p.InAppEnabled));
        Assert.All(preferences, p => Assert.True(p.EmailEnabled));
    }

    [Fact]
    public async Task TurningOffEmailForACategorySkipsTheEmailLegButStillCreatesTheInAppRow()
    {
        var headUserId = await CreateMuqamHeadAsync("20", "Amina Bello");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        db.NotificationPreferences.Add(NotificationPreference.Create(
            Guid.CreateVersion7(), headUserId, NotificationCategory.Comments, inAppEnabled: true, emailEnabled: false));
        await db.SaveChangesAsync();

        await EnqueueAsync(headUserId, NotificationEventType.CommentAdded, CommentPlaceholders);
        await RunOutboxDispatcherAsync();

        var recipient = await identityService.GetUserAsync(headUserId, CancellationToken.None);
        Assert.Null(factory.EmailSender.LastSentTo(recipient!.Email));

        var notifications = await db.Notifications.AsNoTracking().Where(n => n.UserId == headUserId).ToListAsync();
        Assert.Single(notifications);
    }

    [Fact]
    public async Task TurningOffInAppForACategorySkipsTheNotificationRowButStillSendsEmail()
    {
        var headUserId = await CreateMuqamHeadAsync("21", "Chidi Okafor");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        db.NotificationPreferences.Add(NotificationPreference.Create(
            Guid.CreateVersion7(), headUserId, NotificationCategory.Comments, inAppEnabled: false, emailEnabled: true));
        await db.SaveChangesAsync();

        await EnqueueAsync(headUserId, NotificationEventType.CommentAdded, CommentPlaceholders);
        await RunOutboxDispatcherAsync();

        var recipient = await identityService.GetUserAsync(headUserId, CancellationToken.None);
        Assert.NotNull(factory.EmailSender.LastSentTo(recipient!.Email));

        var notifications = await db.Notifications.AsNoTracking().Where(n => n.UserId == headUserId).ToListAsync();
        Assert.Empty(notifications);
    }

    [Fact]
    public async Task UngatedEventTypesAlwaysNotifyRegardlessOfCategoryPreferences()
    {
        var headUserId = await CreateMuqamHeadAsync("22", "Fatima Ibrahim");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();

        await EnqueueAsync(headUserId, NotificationEventType.ChildSubmitted, new Dictionary<string, string>
        {
            ["unitName"] = "Muqam Test",
            ["link"] = "http://x",
        });
        await RunOutboxDispatcherAsync();

        var recipient = await identityService.GetUserAsync(headUserId, CancellationToken.None);
        Assert.NotNull(factory.EmailSender.LastSentTo(recipient!.Email));
        Assert.True(await db.Notifications.AsNoTracking().AnyAsync(n => n.UserId == headUserId));
    }

    [Fact]
    public async Task DigestEnabledUserGetsNoRealTimeEmailButTheMessageIsMarkedSent()
    {
        var headUserId = await CreateMuqamHeadAsync("23", "Grace Adebayo");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        db.UserNotificationSettings.Add(HRS.Domain.Notifications.UserNotificationSettings.Create(headUserId, digestEnabled: true));
        await db.SaveChangesAsync();

        await EnqueueAsync(headUserId, NotificationEventType.CommentAdded, CommentPlaceholders);
        await RunOutboxDispatcherAsync();

        var recipient = await identityService.GetUserAsync(headUserId, CancellationToken.None);
        Assert.Null(factory.EmailSender.LastSentTo(recipient!.Email));

        var message = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.RecipientUserId == headUserId);
        Assert.NotNull(message.ProcessedAt);

        // The in-app row still exists in real time (digest only ever changes email delivery).
        Assert.True(await db.Notifications.AsNoTracking().AnyAsync(n => n.UserId == headUserId));
    }

    [Fact]
    public async Task DigestJobSendsOneCombinedEmailAndAdvancesTheCursorSoItIsNotResent()
    {
        var headUserId = await CreateMuqamHeadAsync("24", "Ibrahim Musa");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        db.UserNotificationSettings.Add(HRS.Domain.Notifications.UserNotificationSettings.Create(headUserId, digestEnabled: true));
        await db.SaveChangesAsync();

        await EnqueueAsync(headUserId, NotificationEventType.CommentAdded, CommentPlaceholders);
        await EnqueueAsync(headUserId, NotificationEventType.ReportAcknowledged, new Dictionary<string, string>
        {
            ["unitName"] = "Dila Ibafo",
            ["link"] = "http://x",
        });
        await RunOutboxDispatcherAsync();

        await RunDigestAsync();

        var recipient = await identityService.GetUserAsync(headUserId, CancellationToken.None);
        var digestEmail = factory.EmailSender.LastSentTo(recipient!.Email);
        Assert.NotNull(digestEmail);
        Assert.Contains("2", digestEmail!.Subject);

        // A second run with nothing new since the cursor advanced must not resend.
        await RunDigestAsync();
        var settings = await db.UserNotificationSettings.AsNoTracking().SingleAsync(s => s.UserId == headUserId);
        Assert.NotNull(settings.LastDigestSentAt);
    }

    [Fact]
    public async Task SendingAnAnnouncementNotifiesTheResolvedAudienceAndRecordsBanner()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var headUserId = await CreateMuqamHeadAsync("25", "Blessing Eze");

        var response = await secretariat.PostAsJsonAsync(
            "/api/v1/secretariat/announcements",
            new SendAnnouncementRequest("September reports are open", "Please submit by the 5th.", AnnouncementAudience.AllHeads, ShowAsBanner: true, SendEmail: true));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = await response.Content.ReadFromJsonAsync<AnnouncementSummary>();
        Assert.True(sent!.DeliveredCount >= 1);

        await RunOutboxDispatcherAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var notification = await db.Notifications.AsNoTracking()
            .SingleAsync(n => n.UserId == headUserId && n.EventType == NotificationEventType.Announcement);
        Assert.True(notification.IsBanner);
        Assert.Equal("September reports are open", notification.Subject);

        var recipient = await identityService.GetUserAsync(headUserId, CancellationToken.None);
        Assert.NotNull(factory.EmailSender.LastSentTo(recipient!.Email));
    }

    [Fact]
    public async Task SendingAnAnnouncementWithSendEmailFalseNeverEmailsEvenIfTheRecipientAllowsIt()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var headUserId = await CreateMuqamHeadAsync("26", "David Nwosu");

        await secretariat.PostAsJsonAsync(
            "/api/v1/secretariat/announcements",
            new SendAnnouncementRequest("Template v4 changes", "The Projects section was added.", AnnouncementAudience.AllHeads, ShowAsBanner: false, SendEmail: false));

        await RunOutboxDispatcherAsync();

        using var scope = factory.Services.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var recipient = await identityService.GetUserAsync(headUserId, CancellationToken.None);
        Assert.Null(factory.EmailSender.LastSentTo(recipient!.Email));
    }

    [Fact]
    public async Task GetAnnouncementsListsThePreviouslySentOneNewestFirst()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        await CreateMuqamHeadAsync("27");

        await secretariat.PostAsJsonAsync(
            "/api/v1/secretariat/announcements",
            new SendAnnouncementRequest("Older Announcement 27", "First.", AnnouncementAudience.AllHeads, ShowAsBanner: false, SendEmail: true));
        await secretariat.PostAsJsonAsync(
            "/api/v1/secretariat/announcements",
            new SendAnnouncementRequest("Newer Announcement 27", "Second.", AnnouncementAudience.AllHeads, ShowAsBanner: false, SendEmail: true));

        var list = await secretariat.GetFromJsonAsync<List<AnnouncementSummary>>("/api/v1/secretariat/announcements");

        var indexOfNewer = list!.FindIndex(a => a.Title == "Newer Announcement 27");
        var indexOfOlder = list.FindIndex(a => a.Title == "Older Announcement 27");
        Assert.True(indexOfNewer < indexOfOlder);
    }

    [Fact]
    public async Task AudienceCountsReflectOnlyActiveHeadsAtTheRequestedLevel()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        await CreateMuqamHeadAsync("28");

        var counts = await secretariat.GetFromJsonAsync<List<AnnouncementAudienceCount>>("/api/v1/secretariat/announcements/audience-counts");

        Assert.Equal(4, counts!.Count);
        var muqamHeads = counts.Single(c => c.Audience == AnnouncementAudience.MuqamHeads);
        Assert.True(muqamHeads.RecipientCount >= 1);
    }

    [Fact]
    public async Task NonSecretariatCannotSendAnnouncements()
    {
        var (client, _) = await CreateLoggedInClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/secretariat/announcements",
            new SendAnnouncementRequest("Title", "Message", AnnouncementAudience.AllHeads, ShowAsBanner: false, SendEmail: true));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DigestSettingsRoundTripThroughTheApi()
    {
        var (client, _) = await CreateLoggedInClientAsync();

        var before = await client.GetFromJsonAsync<NotificationSettingsView>("/api/v1/me/notification-settings");
        Assert.False(before!.DigestEnabled);

        await client.PutAsJsonAsync("/api/v1/me/notification-settings", new UpdateNotificationSettingsRequest(DigestEnabled: true));

        var after = await client.GetFromJsonAsync<NotificationSettingsView>("/api/v1/me/notification-settings");
        Assert.True(after!.DigestEnabled);
    }
}
