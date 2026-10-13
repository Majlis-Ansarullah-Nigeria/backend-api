using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Notifications.NotificationTemplates;
using HRS.Domain.Access;
using HRS.Domain.Notifications;
using HRS.Domain.Organization;

namespace HRS.Api.IntegrationTests;

/// <summary>FR-NOT-003: the Secretariat's notification template editor.</summary>
[Collection(ApiTestCollection.Name)]
public class NotificationTemplatesEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Notif1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<HttpClient> CreatePlainUserClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Notif2";
        await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    [Fact]
    public async Task ListingReturnsAllElevenSeededDefaultsOnceEach()
    {
        using var secretariat = await CreateSecretariatClientAsync();

        var templates = await (await secretariat.GetAsync(new Uri("/api/v1/secretariat/notification-templates", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<NotificationTemplateSummary>>();

        // 9 from FR-NOT-002/M8.1 plus M8.3's two later additions: Escalated (FR-NOT-005) and
        // Announcement (FR-NOT-006).
        Assert.Equal(11, templates!.Count);
        Assert.Equal(11, templates.Select(t => t.EventType).Distinct().Count());
        Assert.Contains(templates, t => t.EventType == NotificationEventType.CommentAdded);
        Assert.Contains(templates, t => t.EventType == NotificationEventType.Escalated);
        Assert.Contains(templates, t => t.EventType == NotificationEventType.Announcement);
    }

    [Fact]
    public async Task ListingIsForbiddenForNonSecretariat()
    {
        using var client = await CreatePlainUserClientAsync();

        var response = await client.GetAsync(new Uri("/api/v1/secretariat/notification-templates", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SecretariatCanEditATemplatesSubjectAndBody()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var templates = await (await secretariat.GetAsync(new Uri("/api/v1/secretariat/notification-templates", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<NotificationTemplateSummary>>();
        var target = templates!.First(t => t.EventType == NotificationEventType.Overdue);

        var response = await secretariat.PutAsJsonAsync(
            $"/api/v1/secretariat/notification-templates/{target.Id}",
            new UpdateNotificationTemplateRequest("Custom subject for {{unitName}}", "Custom body, {{headName}}."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<NotificationTemplateSummary>();
        Assert.Equal("Custom subject for {{unitName}}", updated!.Subject);
        Assert.Equal("Custom body, {{headName}}.", updated.Body);

        var refetched = await (await secretariat.GetAsync(new Uri("/api/v1/secretariat/notification-templates", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<NotificationTemplateSummary>>();
        Assert.Contains(refetched!, t => t.Id == target.Id && t.Subject == "Custom subject for {{unitName}}");
    }

    [Fact]
    public async Task UpdatingRejectsAnEmptySubjectOrBody()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var templates = await (await secretariat.GetAsync(new Uri("/api/v1/secretariat/notification-templates", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<NotificationTemplateSummary>>();
        var target = templates!.First();

        var response = await secretariat.PutAsJsonAsync(
            $"/api/v1/secretariat/notification-templates/{target.Id}", new UpdateNotificationTemplateRequest("   ", "Body"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatingIsForbiddenForNonSecretariat()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var templates = await (await secretariat.GetAsync(new Uri("/api/v1/secretariat/notification-templates", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<NotificationTemplateSummary>>();
        var target = templates!.First();

        using var client = await CreatePlainUserClientAsync();
        var response = await client.PutAsJsonAsync(
            $"/api/v1/secretariat/notification-templates/{target.Id}", new UpdateNotificationTemplateRequest("Subject", "Body"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdatingAnUnknownTemplateReturnsNotFound()
    {
        using var secretariat = await CreateSecretariatClientAsync();

        var response = await secretariat.PutAsJsonAsync(
            $"/api/v1/secretariat/notification-templates/{Guid.NewGuid()}", new UpdateNotificationTemplateRequest("Subject", "Body"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
