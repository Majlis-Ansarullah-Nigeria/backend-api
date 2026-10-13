using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Notifications;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class WindowsEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow.AddDays(1);

    private static CreateReportTypeRequest MonthlyReport(string name, params UnitLevelCode[] levels) => new(
        name,
        "Monthly activity figures",
        ReportTypeCategory.GeneralMonthly,
        ReportTypeFrequency.Monthly,
        levels,
        UnitLevelCode.National,
        1,
        AllowAttachments: true, AllowNilReport: false);

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Win1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<HttpClient> CreatePlainUserClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Win2";
        await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private static async Task<Guid> CreateReportTypeAsync(HttpClient client, string name, params UnitLevelCode[] levels)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MonthlyReport(name, levels)))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        return created!.Id;
    }

    private static CreateSubmissionWindowRequest ValidWindow(params WindowDueDate[] dueDates) => new(
        "October 2026",
        Now,
        3,
        Now.AddDays(30),
        dueDates);

    // Opens/closes relative to the real current time (not the test-wide `Now` offset, which is
    // always a day ahead), so the window has genuinely already hit its hard close.
    private static CreateSubmissionWindowRequest AlreadyClosedWindow(params WindowDueDate[] dueDates) => new(
        "A Closed Period",
        DateTimeOffset.UtcNow.AddDays(-30),
        3,
        DateTimeOffset.UtcNow.AddDays(-1),
        dueDates);

    // "W"-prefixed codes (WZ/WD/WM), not the plain Z/D/M codes other integration test files use —
    // every test in HRS.Api.IntegrationTests shares one Testcontainers Postgres, so unit codes
    // must be namespaced per file to avoid (ParentId, Code) collisions across files.
    private static async Task<Guid> CreateMuqamUnitAsync(HttpClient client, string suffix)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"WZ{suffix}", $"Window Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"WD{suffix}", $"Window Dila {suffix}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var muqam = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"WM{suffix}", $"Window Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dila!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();

        return muqam!.Id;
    }

    [Fact]
    public async Task CreatingAWindowRequiresSecretariat()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Gate Report", UnitLevelCode.Muqam);

        using var client = await CreatePlainUserClientAsync();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows",
            ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SecretariatCanCreateAWindow()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Valid Window Report", UnitLevelCode.Muqam);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows",
            ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();
        Assert.Equal("October 2026", result!.Window.PeriodLabel);
        Assert.Single(result.Window.DueDates);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task CreatingAWindowForAnUnknownReportTypeReturnsNotFound()
    {
        using var client = await CreateSecretariatClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{Guid.NewGuid()}/windows",
            ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreatingAWindowForAnArchivedReportTypeFails()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Archived Window Report", UnitLevelCode.Muqam);
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/archive", UriKind.Relative), null);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows",
            ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ADueDateForANonApplicableLevelFails()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Muqam Only Report", UnitLevelCode.Muqam);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows",
            ValidWindow(new WindowDueDate(UnitLevelCode.Zone, Now.AddDays(10))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AParentDueBeforeItsChildProducesAWarningButStillSaves()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Warning Report", UnitLevelCode.Muqam, UnitLevelCode.Dila);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows",
            ValidWindow(
                new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10)),
                new WindowDueDate(UnitLevelCode.Dila, Now.AddDays(5))));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();
        Assert.Single(result!.Warnings);
    }

    [Fact]
    public async Task GetSubmissionWindowReturnsTheCreatedWindow()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Fetchable Window Report", UnitLevelCode.Muqam);
        var created = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/windows",
                ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10)))))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        var response = await client.GetAsync(
            new Uri($"/api/v1/report-types/{reportTypeId}/windows/{created!.Window.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSubmissionWindowForAnUnknownIdReturnsNotFound()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Unknown Window Report", UnitLevelCode.Muqam);

        var response = await client.GetAsync(
            new Uri($"/api/v1/report-types/{reportTypeId}/windows/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSubmissionWindowsListsWindowsForTheReportTypeOnly()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Listed Window Report", UnitLevelCode.Muqam);
        var otherReportTypeId = await CreateReportTypeAsync(client, "Other Window Report", UnitLevelCode.Muqam);
        await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows", ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))));
        await client.PostAsJsonAsync(
            $"/api/v1/report-types/{otherReportTypeId}/windows", ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))));

        var response = await client.GetAsync(new Uri($"/api/v1/report-types/{reportTypeId}/windows", UriKind.Relative));
        var windows = await response.Content.ReadFromJsonAsync<List<SubmissionWindowSummary>>();

        var window = Assert.Single(windows!);
        Assert.Equal(reportTypeId, window.ReportTypeId);
    }

    [Fact]
    public async Task UpdateSubmissionWindowReplacesItsDueDates()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Updatable Window Report", UnitLevelCode.Muqam, UnitLevelCode.Dila);
        var created = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/windows",
                ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10)))))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        var update = new UpdateSubmissionWindowRequest(
            "November 2026", Now.AddDays(1), 5, Now.AddDays(40), [new WindowDueDate(UnitLevelCode.Dila, Now.AddDays(15))]);
        var response = await client.PutAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows/{created!.Window.Id}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();
        Assert.Equal("November 2026", result!.Window.PeriodLabel);
        var dueDate = Assert.Single(result.Window.DueDates);
        Assert.Equal(UnitLevelCode.Dila, dueDate.Level);
    }

    [Fact]
    public async Task CreatingAWindowWithoutReminderSettingsDefaultsThem()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Default Reminders Report", UnitLevelCode.Muqam);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows",
            ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))));

        var result = await response.Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();
        Assert.True(result!.Window.ReminderSettings.RemindFiveDaysBefore);
        Assert.True(result.Window.ReminderSettings.RemindTwoDaysBefore);
        Assert.True(result.Window.ReminderSettings.RemindOnDueDay);
        Assert.True(result.Window.ReminderSettings.EscalateToParentAfterThreeDays);
        Assert.False(result.Window.ReminderSettings.EscalateToZoneAfterSevenDays);
    }

    [Fact]
    public async Task UpdateSubmissionWindowReplacesItsReminderSettings()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Custom Reminders Report", UnitLevelCode.Muqam);
        var created = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/windows",
                ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10)))))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        var customSettings = new ReminderSettings(false, false, false, false, true);
        var update = new UpdateSubmissionWindowRequest(
            "October 2026", Now, 3, Now.AddDays(30), [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))],
            ReminderSettings: customSettings);
        var response = await client.PutAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows/{created!.Window.Id}", update);

        var result = await response.Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();
        Assert.False(result!.Window.ReminderSettings.RemindFiveDaysBefore);
        Assert.True(result.Window.ReminderSettings.EscalateToZoneAfterSevenDays);
    }

    [Fact]
    public async Task UpdateSubmissionWindowForAnUnknownIdReturnsNotFound()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Unknown Update Window Report", UnitLevelCode.Muqam);

        var update = new UpdateSubmissionWindowRequest(
            "November 2026", Now, 3, Now.AddDays(30), [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))]);
        var response = await client.PutAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows/{Guid.NewGuid()}", update);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnAdHocWindowCanTargetAHandPickedListOfUnits()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Hand Picked Window Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(client, "01");

        var request = new CreateSubmissionWindowRequest(
            "Flood Survey", Now, 3, Now.AddDays(30), [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))], [muqamId]);
        var response = await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();
        Assert.Equal([muqamId], result!.Window.TargetUnitIds);
        Assert.Null(result.Window.ScheduleId);
    }

    [Fact]
    public async Task AnUnknownTargetUnitIdFails()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Unknown Target Report", UnitLevelCode.Muqam);

        var request = new CreateSubmissionWindowRequest(
            "Flood Survey", Now, 3, Now.AddDays(30), [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))], [Guid.NewGuid()]);
        var response = await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ATargetUnitAtALevelWithNoDueDateFails()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "No Due Date Target Report", UnitLevelCode.Muqam, UnitLevelCode.Dila);
        var muqamId = await CreateMuqamUnitAsync(client, "02");

        // Only Dila has a due date — the targeted Muqam's level has none.
        var request = new CreateSubmissionWindowRequest(
            "Flood Survey", Now, 3, Now.AddDays(30), [new WindowDueDate(UnitLevelCode.Dila, Now.AddDays(10))], [muqamId]);
        var response = await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnArchivedTargetUnitFails()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Archived Target Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(client, "03");
        await client.DeleteAsync(new Uri($"/api/v1/units/{muqamId}", UriKind.Relative));

        var request = new CreateSubmissionWindowRequest(
            "Flood Survey", Now, 3, Now.AddDays(30), [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))], [muqamId]);
        var response = await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GrantingAnExtensionRequiresSecretariat()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Extension Gate Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "04");
        var created = await (await secretariat.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/windows",
                ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10)))))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        using var client = await CreatePlainUserClientAsync();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows/{created!.Window.Id}/extensions",
            new GrantWindowExtensionRequest([muqamId], Now.AddDays(20), Now.AddDays(25), "Reason"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SecretariatCanGrantAnExtensionToAUnit()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Extendable Window Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(client, "05");
        var created = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/windows",
                ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10)))))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        var newDueAt = Now.AddDays(20);
        var newClosesAt = Now.AddDays(25);
        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows/{created!.Window.Id}/extensions",
            new GrantWindowExtensionRequest([muqamId], newDueAt, newClosesAt, "Flooding delayed the count."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SubmissionWindowSummary>();
        var extension = Assert.Single(result!.Extensions);
        Assert.Equal(muqamId, extension.UnitId);
        Assert.Equal(newDueAt, extension.NewDueAt);
        Assert.Equal(newClosesAt, extension.NewClosesAt);
    }

    [Fact]
    public async Task ExtensionNewClosesAtBeforeNewDueAtFails()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Invalid Extension Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(client, "06");
        var created = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/windows",
                ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10)))))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows/{created!.Window.Id}/extensions",
            new GrantWindowExtensionRequest([muqamId], Now.AddDays(25), Now.AddDays(20), "Reason"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExtensionForAUnitNotTargetedByTheWindowFails()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Scoped Extension Report", UnitLevelCode.Muqam);
        var targetedMuqamId = await CreateMuqamUnitAsync(client, "07a");
        var otherMuqamId = await CreateMuqamUnitAsync(client, "07b");
        var created = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/windows",
                new CreateSubmissionWindowRequest(
                    "Flood Survey", Now, 3, Now.AddDays(30), [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))], [targetedMuqamId])))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows/{created!.Window.Id}/extensions",
            new GrantWindowExtensionRequest([otherMuqamId], Now.AddDays(20), Now.AddDays(25), "Reason"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReopeningRequiresSecretariat()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Reopen Gate Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "08");
        var created = await (await secretariat.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/windows",
                AlreadyClosedWindow(new WindowDueDate(UnitLevelCode.Muqam, DateTimeOffset.UtcNow.AddDays(-20)))))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        using var client = await CreatePlainUserClientAsync();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows/{created!.Window.Id}/reopen",
            new ReopenWindowRequest([muqamId], DateTimeOffset.UtcNow.AddDays(5), "Reason"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SecretariatCanReopenAClosedWindowForAUnit()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Reopenable Window Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(client, "09");
        var created = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/windows",
                AlreadyClosedWindow(new WindowDueDate(UnitLevelCode.Muqam, DateTimeOffset.UtcNow.AddDays(-20)))))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        var reopenedUntil = DateTimeOffset.UtcNow.AddDays(5);
        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows/{created!.Window.Id}/reopen",
            new ReopenWindowRequest([muqamId], reopenedUntil, "Late due to a connectivity outage."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SubmissionWindowSummary>();
        var reopen = Assert.Single(result!.Reopens);
        Assert.Equal(muqamId, reopen.UnitId);
        Assert.Equal(reopenedUntil, reopen.ReopenedUntil);
    }

    [Fact]
    public async Task ReopeningAWindowThatHasNotClosedYetFails()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Not Yet Closed Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(client, "10");
        var created = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/windows",
                ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10)))))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows/{created!.Window.Id}/reopen",
            new ReopenWindowRequest([muqamId], Now.AddDays(40), "Reason"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ReopenedUntilBeforeTheCurrentCloseFails()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Insufficient Reopen Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(client, "11");
        var closesAt = DateTimeOffset.UtcNow.AddDays(-1);
        var created = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/windows",
                AlreadyClosedWindow(new WindowDueDate(UnitLevelCode.Muqam, DateTimeOffset.UtcNow.AddDays(-20)))))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows/{created!.Window.Id}/reopen",
            new ReopenWindowRequest([muqamId], closesAt.AddDays(-1), "Reason"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>M8.1: runs the same dispatcher the Worker's recurring job runs, against the test
    /// database, so these tests can assert against factory.EmailSender. Loops until a run sends
    /// nothing more: every test in HRS.Api.IntegrationTests shares one never-reset Postgres, so
    /// by the time later test classes run there can be a real backlog of other tests' pending
    /// messages ahead of this one's in the dispatcher's oldest-first batch.</summary>
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

    [Fact]
    public async Task GrantingAnExtensionNotifiesTheUnitsCurrentHead()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Extension Notify Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "08");
        var (_, headUserId, headEmail) = await CreateAuthenticatedUserWithEmailAsync();
        await factory.CreateRoleAssignmentAsync(headUserId, muqamId, RoleName.UnitHead);
        var created = await (await secretariat.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/windows",
                ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10)))))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        await secretariat.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows/{created!.Window.Id}/extensions",
            new GrantWindowExtensionRequest([muqamId], Now.AddDays(20), Now.AddDays(25), "Flooding delayed the count."));

        await RunOutboxDispatcherAsync();

        var sent = factory.EmailSender.LastSentTo(headEmail);
        Assert.NotNull(sent);
        Assert.Contains("Flooding delayed the count.", sent!.TextBody);
    }

    private async Task<(HttpClient Client, Guid UserId, string Email)> CreateAuthenticatedUserWithEmailAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Win3";
        var userId = await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, userId, email);
    }
}
