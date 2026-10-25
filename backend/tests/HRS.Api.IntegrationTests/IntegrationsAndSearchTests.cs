using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Admin.Features;
using HRS.Application.Features.Admin.Webhooks;
using HRS.Application.Features.Analytics;
using HRS.Application.Features.Audit;
using HRS.Application.Features.Notifications;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Search;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Domain.Access;
using HRS.Domain.Admin;
using HRS.Domain.Notifications;
using HRS.Domain.Organization;
using HRS.Domain.Templates;
using HRS.Domain.Webhooks;
using HRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

/// <summary>FR-ADM-003 (webhooks), FR-ADM-004 (feature flags) and FR-SRC-001 (global search).</summary>
[Collection(ApiTestCollection.Name)]
public class IntegrationsAndSearchTests(ApiFactory factory)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private async Task<(HttpClient Client, Guid UserId)> CreateClientAsync(RoleName? role, string name = "Test User")
    {
        var email = $"{Guid.NewGuid():N}@example.org";
        const string password = "Correct-Horse-Integr1";
        var userId = await factory.CreateUserAsync(email, password, name: name);
        if (role is { } r)
        {
            await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, r);
        }

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        return (client, userId);
    }

    private static TemplateField Field(string key, TemplateFieldType type, string label, bool confidential = false) =>
        new(key, type, label, null, false, null, null, null, null, null, confidential,
            null, null, null, AggregationRule.None, null, null, null);

    private sealed record Setup(Guid ReportTypeId, Guid WindowId, Guid DilaId, Guid MuqamId, Guid OtherMuqamId);

    /// <summary>A report type with one open window, and a Zone → Dila → two Muqamaat branch.
    /// "WH"/"GS"-prefixed codes keep this file's units apart in the shared database.</summary>
    private static async Task<Setup> SetUpAsync(HttpClient secretariat, string prefix, string suffix)
    {
        var type = await (await secretariat.PostAsJsonAsync("/api/v1/report-types", new CreateReportTypeRequest(
                $"{prefix} Report {suffix}", "Monthly figures", ReportTypeCategory.GeneralMonthly, ReportTypeFrequency.Monthly,
                [UnitLevelCode.Muqam], UnitLevelCode.National, ObserverDepth: 1, AllowAttachments: false, AllowNilReport: false)))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();

        var schema = new TemplateSchema(
        [
            new TemplateSection("figures", "Figures",
            [
                Field("members", TemplateFieldType.Number, "Total members"),
                Field("welfare_cases", TemplateFieldType.Number, "Welfare cases", confidential: true),
            ]),
        ]);
        var draft = await (await secretariat.PostAsJsonAsync(
                $"/api/v1/report-types/{type!.Id}/template-versions", new UpdateTemplateSchemaRequest(schema)))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await secretariat.PostAsync(new Uri($"/api/v1/report-types/{type.Id}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);

        var window = await (await secretariat.PostAsJsonAsync($"/api/v1/report-types/{type.Id}/windows", new CreateSubmissionWindowRequest(
                $"{prefix} Period {suffix}", Now.AddDays(-1), 3, Now.AddDays(20), [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))])))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        async Task<Guid> UnitAsync(string code, string name, Guid levelId, Guid parentId) =>
            (await (await secretariat.PostAsJsonAsync("/api/v1/units",
                    new CreateUnitRequest(code, name, levelId, parentId, null, null, null, null, null)))
                .Content.ReadFromJsonAsync<UnitSummary>())!.Id;

        var zoneId = await UnitAsync($"{prefix}Z{suffix}", $"{prefix} Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId);
        var dilaId = await UnitAsync($"{prefix}D{suffix}", $"{prefix} Dila {suffix}", OrganizationSeedIds.DilaLevelId, zoneId);
        var muqamId = await UnitAsync($"{prefix}M{suffix}", $"{prefix} Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dilaId);
        var otherId = await UnitAsync($"{prefix}N{suffix}", $"{prefix} Other {suffix}", OrganizationSeedIds.MuqamLevelId, dilaId);

        return new Setup(type.Id, window!.Window.Id, dilaId, muqamId, otherId);
    }

    private static async Task SaveAndSubmitAsync(HttpClient head, Guid windowId, Guid unitId, Dictionary<string, object?> answers)
    {
        var url = $"/api/v1/reports/{windowId}?unit={unitId}";
        var current = await head.GetAsync(new Uri(url, UriKind.Relative));
        using var message = new HttpRequestMessage(HttpMethod.Put, new Uri(url, UriKind.Relative))
        {
            Content = JsonContent.Create(new SaveReportDraftRequest(answers)),
        };
        if (current.Headers.ETag is { } etag)
        {
            message.Headers.TryAddWithoutValidation("If-Match", etag.Tag);
        }

        Assert.True((await head.SendAsync(message)).IsSuccessStatusCode);
        var submitted = await head.PostAsJsonAsync($"/api/v1/reports/{windowId}/submit?unit={unitId}", new SubmitReportRequest(Confirmed: true));
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
    }

    private async Task<int> DispatchWebhooksAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        return await WebhookDispatcher.RunAsync(db, factory.WebhookSender, clock, CancellationToken.None);
    }

    [Fact]
    public async Task OnlyTheSystemAdministratorSwitchesModulesAndASwitchedOffModuleIsRefusedServerSide()
    {
        var (admin, adminId) = await CreateClientAsync(RoleName.SysAdmin);
        var (secretariat, _) = await CreateClientAsync(RoleName.Secretariat);
        try
        {
            var view = await secretariat.GetFromJsonAsync<FeatureFlagsView>("/api/v1/feature-flags");
            Assert.False(view!.CanEdit);
            Assert.Equal(Enum.GetValues<FeatureModule>(), view.Flags.Select(f => f.Module).Order());
            Assert.All(view.Flags, f => Assert.True(f.Enabled));

            var refused = await secretariat.PutAsJsonAsync($"/api/v1/feature-flags/{FeatureModule.Dashboards}", new SetFeatureFlagRequest(false));
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

            var off = await admin.PutAsJsonAsync($"/api/v1/feature-flags/{FeatureModule.Dashboards}", new SetFeatureFlagRequest(false));
            Assert.Equal(HttpStatusCode.OK, off.StatusCode);

            var blocked = await secretariat.GetAsync(new Uri("/api/v1/dashboards/compliance", UriKind.Relative));
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
            using (var problem = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync()))
            {
                Assert.Equal("FEATURE_DISABLED", problem.RootElement.GetProperty("code").GetString());
            }

            var access = await secretariat.GetFromJsonAsync<DashboardAccess>("/api/v1/dashboards/access");
            Assert.False(access!.HasCompliance);
            Assert.False(access.HasIndicators);
            Assert.True(access.HasCompiledReports);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.AuditEvents.AnyAsync(e => e.ActorUserId == adminId && e.Action == AuditActions.FeatureToggled));
        }
        finally
        {
            await admin.PutAsJsonAsync($"/api/v1/feature-flags/{FeatureModule.Dashboards}", new SetFeatureFlagRequest(true));
        }

        Assert.Equal(HttpStatusCode.OK, (await secretariat.GetAsync(new Uri("/api/v1/dashboards/compliance", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task WithWebPushSwitchedOffNewSubscriptionsAreRefusedAndNothingIsPushed()
    {
        var (admin, _) = await CreateClientAsync(RoleName.SysAdmin);
        var (client, userId) = await CreateClientAsync(null);
        var endpoint = $"https://push.example.org/{Guid.NewGuid():N}";
        await client.PostAsJsonAsync("/api/v1/me/push-subscriptions", new SavePushSubscriptionRequest(endpoint, "p256dh", "auth"));

        await admin.PutAsJsonAsync($"/api/v1/feature-flags/{FeatureModule.WebPush}", new SetFeatureFlagRequest(false));
        try
        {
            var refused = await client.PostAsJsonAsync("/api/v1/me/push-subscriptions",
                new SavePushSubscriptionRequest($"https://push.example.org/{Guid.NewGuid():N}", "p256dh", "auth"));
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

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
            await OutboxDispatcher.RunAsync(db, identityService, factory.EmailSender, factory.PushSender, factory.Broadcaster, clock, CancellationToken.None);

            Assert.Null(factory.PushSender.LastSentTo(endpoint));
            Assert.True(await db.Notifications.AnyAsync(n => n.UserId == userId));
        }
        finally
        {
            await admin.PutAsJsonAsync($"/api/v1/feature-flags/{FeatureModule.WebPush}", new SetFeatureFlagRequest(true));
        }
    }

    [Fact]
    public async Task WebhooksAreSystemAdministratorOnlyAndTheSecretIsShownOnceAndNeverAudited()
    {
        var (admin, adminId) = await CreateClientAsync(RoleName.SysAdmin);
        var (secretariat, _) = await CreateClientAsync(RoleName.Secretariat);
        const string url = "https://hooks.example.org/hrs-admin";

        Assert.Equal(HttpStatusCode.Forbidden,
            (await secretariat.PostAsJsonAsync("/api/v1/webhooks", new SaveWebhookRequest(url, [WebhookEvent.ReportSubmitted]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/v1/webhooks", new SaveWebhookRequest("http://hooks.example.org/plain", [WebhookEvent.ReportSubmitted]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/v1/webhooks", new SaveWebhookRequest(url, [WebhookEvent.Ping]))).StatusCode);

        var created = await (await admin.PostAsJsonAsync("/api/v1/webhooks", new SaveWebhookRequest(url, [WebhookEvent.ReportSubmitted])))
            .Content.ReadFromJsonAsync<WebhookSecretResult>();
        var id = created!.Endpoint.Id;
        try
        {
            Assert.StartsWith("whsec_", created.Secret, StringComparison.Ordinal);
            Assert.Equal(WebhookSigning.Hint(created.Secret), created.Endpoint.SecretHint);

            var listJson = await admin.GetStringAsync(new Uri("/api/v1/webhooks", UriKind.Relative));
            Assert.DoesNotContain(created.Secret, listJson, StringComparison.Ordinal);

            var updated = await (await admin.PutAsJsonAsync($"/api/v1/webhooks/{id}",
                    new SaveWebhookRequest(url, [WebhookEvent.ReportSubmitted, WebhookEvent.ReportAcknowledged], IsActive: false)))
                .Content.ReadFromJsonAsync<WebhookEndpointDto>();
            Assert.Equal((false, 2), (updated!.IsActive, updated.Events.Count));

            var rotated = await (await admin.PostAsync(new Uri($"/api/v1/webhooks/{id}/rotate-secret", UriKind.Relative), null))
                .Content.ReadFromJsonAsync<WebhookSecretResult>();
            Assert.NotEqual(created.Secret, rotated!.Secret);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var audit = await db.AuditEvents.AsNoTracking()
                .Where(e => e.ActorUserId == adminId && e.Action == AuditActions.WebhookChanged).ToListAsync();
            Assert.True(audit.Count >= 3);
            Assert.All(audit, e =>
            {
                Assert.DoesNotContain(created.Secret, (e.BeforeJson ?? "") + (e.AfterJson ?? ""), StringComparison.Ordinal);
                Assert.DoesNotContain(rotated.Secret, (e.BeforeJson ?? "") + (e.AfterJson ?? ""), StringComparison.Ordinal);
            });
        }
        finally
        {
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(new Uri($"/api/v1/webhooks/{id}", UriKind.Relative))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync(new Uri($"/api/v1/webhooks/{id}", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task SubmittingAndAcknowledgingSendSignedPayloadsWithAnswersButNoConfidentialFields()
    {
        var (admin, _) = await CreateClientAsync(RoleName.SysAdmin);
        var (secretariat, _) = await CreateClientAsync(RoleName.Secretariat);
        var setup = await SetUpAsync(secretariat, "WH", "01");
        var (head, headId) = await CreateClientAsync(null);
        await factory.CreateRoleAssignmentAsync(headId, setup.MuqamId, RoleName.UnitHead);

        const string url = "https://hooks.example.org/hrs-reports";
        var created = await (await admin.PostAsJsonAsync("/api/v1/webhooks",
                new SaveWebhookRequest(url, [WebhookEvent.ReportSubmitted, WebhookEvent.ReportAcknowledged])))
            .Content.ReadFromJsonAsync<WebhookSecretResult>();
        try
        {
            await SaveAndSubmitAsync(head, setup.WindowId, setup.MuqamId, new() { ["members"] = 142, ["welfare_cases"] = 3 });

            Guid instanceId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                instanceId = (await db.ReportInstances.AsNoTracking().SingleAsync(i => i.WindowId == setup.WindowId && i.UnitId == setup.MuqamId)).Id;
            }

            var acknowledged = await secretariat.PostAsync(new Uri($"/api/v1/secretariat/inbox/{instanceId}/acknowledge", UriKind.Relative), null);
            Assert.True(acknowledged.IsSuccessStatusCode);

            await DispatchWebhooksAsync();

            var sent = factory.WebhookSender.SentTo(url);
            Assert.Equal(["report.submitted", "report.acknowledged"], sent.Select(r => r.EventName));
            foreach (var request in sent)
            {
                Assert.Equal(WebhookSigning.Sign(created!.Secret, request.Timestamp, request.Body), request.Signature);

                using var body = JsonDocument.Parse(request.Body);
                var report = body.RootElement.GetProperty("report");
                Assert.Equal(instanceId, report.GetProperty("id").GetGuid());
                Assert.Equal("WH Muqam 01", report.GetProperty("unit").GetProperty("name").GetString());
                var answers = body.RootElement.GetProperty("answers").EnumerateArray().ToList();
                var members = Assert.Single(answers);
                Assert.Equal(("members", 142), (members.GetProperty("key").GetString(), members.GetProperty("value").GetInt32()));
            }

            var deliveries = await admin.GetFromJsonAsync<List<WebhookDeliveryDto>>($"/api/v1/webhooks/{created!.Endpoint.Id}/deliveries");
            Assert.Equal(2, deliveries!.Count);
            Assert.All(deliveries, d => Assert.Equal((WebhookDeliveryStatus.Delivered, 1, 200), (d.Status, d.Attempts, d.LastStatusCode)));
        }
        finally
        {
            await admin.DeleteAsync(new Uri($"/api/v1/webhooks/{created!.Endpoint.Id}", UriKind.Relative));
        }
    }

    [Fact]
    public async Task AFailedTestDeliveryIsRetriedLaterAndAnInactiveEndpointsDeliveryIsAbandoned()
    {
        var (admin, _) = await CreateClientAsync(RoleName.SysAdmin);
        const string url = "https://hooks.example.org/hrs-failing";
        factory.WebhookSender.FailingUrls[url] = true;
        var created = await (await admin.PostAsJsonAsync("/api/v1/webhooks", new SaveWebhookRequest(url, [WebhookEvent.ReportSubmitted])))
            .Content.ReadFromJsonAsync<WebhookSecretResult>();
        var id = created!.Endpoint.Id;
        try
        {
            Assert.True((await admin.PostAsync(new Uri($"/api/v1/webhooks/{id}/test", UriKind.Relative), null)).IsSuccessStatusCode);
            await DispatchWebhooksAsync();

            var ping = Assert.Single(factory.WebhookSender.SentTo(url));
            Assert.Equal("ping", ping.EventName);

            var failed = Assert.Single((await admin.GetFromJsonAsync<List<WebhookDeliveryDto>>($"/api/v1/webhooks/{id}/deliveries"))!);
            Assert.Equal((WebhookDeliveryStatus.Pending, 1, 500), (failed.Status, failed.Attempts, failed.LastStatusCode));
            Assert.True(failed.NextAttemptAt > DateTimeOffset.UtcNow.AddSeconds(30));

            // Not due yet: a second run sends nothing more.
            await DispatchWebhooksAsync();
            Assert.Single(factory.WebhookSender.SentTo(url));

            await admin.PutAsJsonAsync($"/api/v1/webhooks/{id}", new SaveWebhookRequest(url, [WebhookEvent.ReportSubmitted], IsActive: false));
            await admin.PostAsync(new Uri($"/api/v1/webhooks/{id}/test", UriKind.Relative), null);
            await DispatchWebhooksAsync();

            Assert.Single(factory.WebhookSender.SentTo(url));
        }
        finally
        {
            factory.WebhookSender.FailingUrls.TryRemove(url, out _);
            await admin.DeleteAsync(new Uri($"/api/v1/webhooks/{id}", UriKind.Relative));
        }
    }

    [Fact]
    public async Task SearchFindsEverythingForTheSecretariatButOnlyTheirOwnScopeForAHead()
    {
        var (secretariat, _) = await CreateClientAsync(RoleName.Secretariat, name: "Gsearch Secretary");
        var setup = await SetUpAsync(secretariat, "GS", "97");
        var (head, headId) = await CreateClientAsync(null, name: "Gsearch Head");
        await factory.CreateRoleAssignmentAsync(headId, setup.MuqamId, RoleName.UnitHead);
        var (dilaHead, dilaHeadId) = await CreateClientAsync(null, name: "Gsearch Dila Head");
        await factory.CreateRoleAssignmentAsync(dilaHeadId, setup.DilaId, RoleName.UnitHead);

        await SaveAndSubmitAsync(head, setup.WindowId, setup.MuqamId, new() { ["members"] = 10 });

        var asSecretariat = await secretariat.GetFromJsonAsync<GlobalSearchResults>("/api/v1/search?q=GS%2097");
        Assert.Contains(asSecretariat!.Units, u => u.Id == setup.MuqamId);
        Assert.Contains(asSecretariat.Units, u => u.Id == setup.OtherMuqamId);
        var reviewHit = Assert.Single(asSecretariat.Reports);
        Assert.Equal((setup.MuqamId, SearchReportAccess.Review), (reviewHit.UnitId, reviewHit.Access));
        Assert.Contains(asSecretariat.ReportTypes, r => r.Id == setup.ReportTypeId);

        var users = await secretariat.GetFromJsonAsync<GlobalSearchResults>("/api/v1/search?q=gsearch%20dila");
        Assert.Equal(dilaHeadId, Assert.Single(users!.Users).Id);

        var asHead = await head.GetFromJsonAsync<GlobalSearchResults>("/api/v1/search?q=GS%2097");
        Assert.Equal(setup.MuqamId, Assert.Single(asHead!.Units).Id);
        Assert.Equal(SearchReportAccess.Own, Assert.Single(asHead.Reports).Access);
        Assert.Empty(asHead.ReportTypes);
        Assert.Empty((await head.GetFromJsonAsync<GlobalSearchResults>("/api/v1/search?q=gsearch"))!.Users);

        var asDilaHead = await dilaHead.GetFromJsonAsync<GlobalSearchResults>("/api/v1/search?q=GS%2097");
        Assert.Equal([setup.DilaId, setup.MuqamId, setup.OtherMuqamId], asDilaHead!.Units.Select(u => u.Id).Order());
        Assert.True(asDilaHead.Units.Single(u => u.Id == setup.MuqamId).IsObserved);
        var observedHit = Assert.Single(asDilaHead.Reports);
        Assert.Equal((setup.MuqamId, SearchReportAccess.Observed), (observedHit.UnitId, observedHit.Access));

        var (stranger, _) = await CreateClientAsync(null);
        var asStranger = await stranger.GetFromJsonAsync<GlobalSearchResults>("/api/v1/search?q=GS%2097");
        Assert.Equal((0, 0, 0, 0), (asStranger!.Units.Count, asStranger.Users.Count, asStranger.Reports.Count, asStranger.ReportTypes.Count));

        var tooShort = await secretariat.GetFromJsonAsync<GlobalSearchResults>("/api/v1/search?q=G");
        Assert.Empty(tooShort!.Units);
    }
}
