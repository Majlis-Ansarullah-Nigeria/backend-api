using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Observer.PrivateNotes;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Domain.Access;
using HRS.Domain.Observer;
using HRS.Domain.Organization;
using HRS.Domain.Templates;
using HRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class NudgeAndPrivateNotesEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CreateReportTypeRequest MonthlyReport(string name, int observerDepth) => new(
        name,
        "Monthly activity figures",
        ReportTypeCategory.GeneralMonthly,
        ReportTypeFrequency.Monthly,
        [UnitLevelCode.Muqam],
        UnitLevelCode.National,
        observerDepth,
        AllowAttachments: true, AllowNilReport: false);

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Nudge1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<(HttpClient Client, Guid UserId, string Email)> CreateAuthenticatedUserAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Nudge2";
        var userId = await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, userId, email);
    }

    private static async Task<Guid> CreateReportTypeAsync(HttpClient client, string name, int observerDepth)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MonthlyReport(name, observerDepth)))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        return created!.Id;
    }

    private static async Task PublishTemplateAsync(HttpClient client, Guid reportTypeId)
    {
        var notes = new TemplateField(
            "notes", TemplateFieldType.ShortText, "Notes", null, false, null, null, null, null, null, false,
            null, null, null, AggregationRule.None, null, null, null);
        var schema = new TemplateSchema([new TemplateSection("section_1", "Section 1", [notes])]);

        var draft = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(schema)))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);
    }

    // "NP"-prefixed codes — every test in HRS.Api.IntegrationTests shares one Testcontainers
    // Postgres, so unit codes must be namespaced per file to avoid (ParentId, Code) collisions.
    private static async Task<(Guid DilaId, Guid MuqamId)> CreateDilaWithOneMuqamAsync(HttpClient client, string suffix)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"NPZ{suffix}", $"Nudge Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"NPD{suffix}", $"Nudge Dila {suffix}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var muqam = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"NPM{suffix}", $"Nudge Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dila!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();

        return (dila.Id, muqam!.Id);
    }

    /// <summary>dueAt in the past, grace/close far in the future — a Draft save against this
    /// window becomes Overdue (Late window + a started-but-unfinished report).</summary>
    private static async Task<Guid> CreateLateWindowAsync(HttpClient client, Guid reportTypeId)
    {
        var request = new CreateSubmissionWindowRequest(
            "Sep 2026", Now.AddDays(-20), 30, Now.AddDays(20), [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(-1))]);
        var created = await (await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        return created!.Window.Id;
    }

    private static async Task<Guid> CreateOpenWindowAsync(HttpClient client, Guid reportTypeId)
    {
        var request = new CreateSubmissionWindowRequest(
            "Sep 2026", Now.AddDays(-5), 3, Now.AddDays(20), [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))]);
        var created = await (await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        return created!.Window.Id;
    }

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, Guid reportId, Guid unitId, Dictionary<string, object?> answers) =>
        client.PutAsJsonAsync($"/api/v1/reports/{reportId}?unit={unitId}", new SaveReportDraftRequest(answers));

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid reportId, Guid unitId) =>
        client.PostAsJsonAsync($"/api/v1/reports/{reportId}/submit?unit={unitId}", new SubmitReportRequest(Confirmed: true));

    private static Task<HttpResponseMessage> NudgeAsync(HttpClient client, Guid childUnitId, Guid windowId) =>
        client.PostAsJsonAsync($"/api/v1/units/{childUnitId}/nudge", new SendNudgeRequest(windowId));

    [Fact]
    public async Task NudgeSendsAReminderEmailToTheOverdueChildsHead()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Nudge Overdue Report", observerDepth: 1);
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "01");
        var windowId = await CreateLateWindowAsync(secretariat, reportTypeId);

        var (muqamClient, muqamUserId, muqamEmail) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(muqamUserId, muqamId, RoleName.UnitHead);
        await SaveAsync(muqamClient, windowId, muqamId, new Dictionary<string, object?> { ["notes"] = "partial" });

        var (dilaClient, dilaUserId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var response = await NudgeAsync(dilaClient, muqamId, windowId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var email = factory.EmailSender.LastSentTo(muqamEmail);
        Assert.NotNull(email);
        Assert.Contains("Nudge Overdue Report", email!.Subject);
    }

    [Fact]
    public async Task NudgeFailsWhenTheChildIsNotOverdue()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Nudge NotOverdue Report", observerDepth: 1);
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "02");
        var windowId = await CreateOpenWindowAsync(secretariat, reportTypeId);

        var (dilaClient, dilaUserId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var response = await NudgeAsync(dilaClient, muqamId, windowId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task NudgeIsRateLimitedWithinTwentyFourHours()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Nudge RateLimit Report", observerDepth: 1);
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "03");
        var windowId = await CreateLateWindowAsync(secretariat, reportTypeId);

        var (muqamClient, muqamUserId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(muqamUserId, muqamId, RoleName.UnitHead);
        await SaveAsync(muqamClient, windowId, muqamId, new Dictionary<string, object?> { ["notes"] = "partial" });

        var (dilaClient, dilaUserId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var first = await NudgeAsync(dilaClient, muqamId, windowId);
        var second = await NudgeAsync(dilaClient, muqamId, windowId);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.True(second.Headers.RetryAfter is not null);
    }

    [Fact]
    public async Task NudgeIsForbiddenForANonObserver()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Nudge Forbidden Report", observerDepth: 1);
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (_, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "04");
        var windowId = await CreateLateWindowAsync(secretariat, reportTypeId);

        var (client, _, _) = await CreateAuthenticatedUserAsync();

        var response = await NudgeAsync(client, muqamId, windowId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<(Guid DilaId, Guid MuqamId, Guid ReportId)> SetUpSubmittedReportAsync(string suffix)
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, $"Private Note Report {suffix}", observerDepth: 1);
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, suffix);
        var windowId = await CreateOpenWindowAsync(secretariat, reportTypeId);

        var (muqamClient, muqamUserId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(muqamUserId, muqamId, RoleName.UnitHead);
        await SaveAsync(muqamClient, windowId, muqamId, new Dictionary<string, object?> { ["notes"] = "All quiet" });
        await SubmitAsync(muqamClient, windowId, muqamId);

        return (dilaId, muqamId, windowId);
    }

    [Fact]
    public async Task AddingAndReadingBackAPrivateNoteWorks()
    {
        var (dilaId, muqamId, reportId) = await SetUpSubmittedReportAsync("05");

        var (dilaClient, dilaUserId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var addResponse = await dilaClient.PostAsJsonAsync(
            $"/api/v1/units/{muqamId}/reports/{reportId}/private-notes", new AddPrivateNoteRequest("Attendance up from last month."));
        var added = await addResponse.Content.ReadFromJsonAsync<PrivateNoteView>();

        var listResponse = await dilaClient.GetAsync(new Uri($"/api/v1/units/{muqamId}/reports/{reportId}/private-notes", UriKind.Relative));
        var notes = await listResponse.Content.ReadFromJsonAsync<List<PrivateNoteView>>();

        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);
        Assert.Equal("Attendance up from last month.", added!.Body);
        Assert.Single(notes!);
        Assert.Equal(added.Id, notes![0].Id);
    }

    [Fact]
    public async Task PrivateNotesAreVisibleToASuccessorHeadOfTheSameParentUnit()
    {
        var (dilaId, muqamId, reportId) = await SetUpSubmittedReportAsync("06");

        var firstDilaUserId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Nudge3");
        await factory.CreateRoleAssignmentAsync(firstDilaUserId, dilaId, RoleName.UnitHead, startsAt: Now.AddDays(-30), endsAt: Now.AddDays(-1));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instance = await db.ReportInstances.FirstAsync(i => i.WindowId == reportId && i.UnitId == muqamId);
        db.PrivateNotes.Add(PrivateNote.Create(
            Guid.CreateVersion7(), instance.Id, dilaId, firstDilaUserId, "Left by the outgoing head.", Now.AddDays(-10)));
        await db.SaveChangesAsync();

        var (successorClient, successorUserId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(successorUserId, dilaId, RoleName.UnitHead, startsAt: Now.AddDays(-1));

        var response = await successorClient.GetAsync(new Uri($"/api/v1/units/{muqamId}/reports/{reportId}/private-notes", UriKind.Relative));
        var notes = await response.Content.ReadFromJsonAsync<List<PrivateNoteView>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(notes!, n => n.Body == "Left by the outgoing head.");
    }

    [Fact]
    public async Task PrivateNotesAreBlockedOnAReportThatIsNotYetSubmitted()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Private Note Draft Report", observerDepth: 1);
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "07");
        var windowId = await CreateOpenWindowAsync(secretariat, reportTypeId);

        var (muqamClient, muqamUserId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(muqamUserId, muqamId, RoleName.UnitHead);
        await SaveAsync(muqamClient, windowId, muqamId, new Dictionary<string, object?> { ["notes"] = "Still drafting" });

        var (dilaClient, dilaUserId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var response = await dilaClient.PostAsJsonAsync(
            $"/api/v1/units/{muqamId}/reports/{windowId}/private-notes", new AddPrivateNoteRequest("Too early"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PrivateNotesAreForbiddenForANonObserver()
    {
        var (_, muqamId, reportId) = await SetUpSubmittedReportAsync("08");

        var (client, _, _) = await CreateAuthenticatedUserAsync();

        var response = await client.GetAsync(new Uri($"/api/v1/units/{muqamId}/reports/{reportId}/private-notes", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
