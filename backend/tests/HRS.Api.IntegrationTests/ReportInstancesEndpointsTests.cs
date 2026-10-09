using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Submissions.ReportInstances;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class ReportInstancesEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Dictionary<string, object?> Answers(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonSerializerOptions.Web)!;

    private static CreateReportTypeRequest MonthlyReport(string name, params UnitLevelCode[] levels) => new(
        name,
        "Monthly activity figures",
        ReportTypeCategory.GeneralMonthly,
        ReportTypeFrequency.Monthly,
        levels,
        UnitLevelCode.National,
        1,
        AllowAttachments: true);

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Rpt1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedUserAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Rpt2";
        var userId = await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, userId);
    }

    private static async Task<Guid> CreateReportTypeAsync(HttpClient client, string name, params UnitLevelCode[] levels)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MonthlyReport(name, levels)))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        return created!.Id;
    }

    private static async Task PublishEmptyTemplateAsync(HttpClient client, Guid reportTypeId)
    {
        var draft = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions",
                new UpdateTemplateSchemaRequest(new TemplateSchema([]))))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);
    }

    // "RI"-prefixed codes — every test in HRS.Api.IntegrationTests shares one Testcontainers
    // Postgres, so unit codes must be namespaced per file to avoid (ParentId, Code) collisions.
    private static async Task<Guid> CreateMuqamUnitAsync(HttpClient client, string suffix)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"RIZ{suffix}", $"Report Instance Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"RID{suffix}", $"Report Instance Dila {suffix}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var muqam = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"RIM{suffix}", $"Report Instance Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dila!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();

        return muqam!.Id;
    }

    private static async Task<Guid> CreateWindowAsync(
        HttpClient client, Guid reportTypeId, DateTimeOffset opensAt, int graceDays, DateTimeOffset closesAt, DateTimeOffset dueAt)
    {
        var request = new CreateSubmissionWindowRequest(
            "Report Instance Test Window", opensAt, graceDays, closesAt, [new WindowDueDate(UnitLevelCode.Muqam, dueAt)]);
        var created = await (await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        return created!.Window.Id;
    }

    private static HttpRequestMessage SaveRequest(Guid windowOrInstanceId, Guid unitId, string answersJson, string? ifMatch = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Put, new Uri($"/api/v1/reports/{windowOrInstanceId}?unit={unitId}", UriKind.Relative))
        {
            Content = JsonContent.Create(new SaveReportDraftRequest(Answers(answersJson))),
        };

        if (ifMatch is not null)
        {
            message.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch}\"");
        }

        return message;
    }

    private async Task MarkInstanceSubmittedAsync(Guid windowId, Guid unitId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instance = await db.ReportInstances.FirstAsync(i => i.WindowId == windowId && i.UnitId == unitId);
        instance.Submit(isLate: false, "REF-TEST", Now);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetBeforeAnySaveReturnsAVirtualNotStartedReport()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Instance Virtual Report", UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "01");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        var response = await client.GetAsync(new Uri($"/api/v1/reports/{windowId}?unit={muqamId}", UriKind.Relative));
        var report = await response.Content.ReadFromJsonAsync<ReportInstanceForEditing>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(report!.Id);
        Assert.Null(report.RowVersion);
        Assert.Equal(ReportInstanceStatus.NotStarted, report.Status);
        Assert.True(report.CanSave);
    }

    [Fact]
    public async Task GetRequiresEditPermissionForTheUnit()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Instance Permission Report", UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "02");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, _) = await CreateAuthenticatedUserAsync();

        var response = await client.GetAsync(new Uri($"/api/v1/reports/{windowId}?unit={muqamId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FirstSaveCreatesTheInstanceAndMovesToDraft()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Instance First Save Report", UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "03");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        using var request = SaveRequest(windowId, muqamId, """{"q1":"hello"}""");
        var response = await client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<SaveReportDraftResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ReportInstanceStatus.Draft, result!.Status);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.NotNull(response.Headers.ETag);
    }

    [Fact]
    public async Task SecondSaveWithAValidIfMatchSucceeds()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Instance Second Save Report", UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "04");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        using var first = SaveRequest(windowId, muqamId, """{"q1":"hello"}""");
        var firstResult = await (await client.SendAsync(first)).Content.ReadFromJsonAsync<SaveReportDraftResult>();

        using var second = SaveRequest(firstResult!.Id, muqamId, """{"q1":"updated"}""", firstResult.RowVersion);
        var secondResponse = await client.SendAsync(second);

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
    }

    [Fact]
    public async Task SaveWithoutIfMatchOnAnExistingInstanceFails()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Instance Missing If Match Report", UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "05");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        using var first = SaveRequest(windowId, muqamId, """{"q1":"hello"}""");
        var firstResult = await (await client.SendAsync(first)).Content.ReadFromJsonAsync<SaveReportDraftResult>();

        using var second = SaveRequest(firstResult!.Id, muqamId, """{"q1":"updated"}""");
        var secondResponse = await client.SendAsync(second);

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }

    [Fact]
    public async Task SaveWithAStaleIfMatchFails()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Instance Stale If Match Report", UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "06");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        using var first = SaveRequest(windowId, muqamId, """{"q1":"hello"}""");
        var firstResult = await (await client.SendAsync(first)).Content.ReadFromJsonAsync<SaveReportDraftResult>();

        // Save again for real, moving the row version on — then retry with the now-stale first one.
        using var second = SaveRequest(firstResult!.Id, muqamId, """{"q1":"updated"}""", firstResult.RowVersion);
        await client.SendAsync(second);

        using var third = SaveRequest(firstResult.Id, muqamId, """{"q1":"stale write"}""", firstResult.RowVersion);
        var thirdResponse = await client.SendAsync(third);

        Assert.Equal(HttpStatusCode.Conflict, thirdResponse.StatusCode);
    }

    [Fact]
    public async Task SaveBeforeTheWindowOpensFails()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Instance Not Open Report", UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "07");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(5), 3, Now.AddDays(30), Now.AddDays(15));

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        using var request = SaveRequest(windowId, muqamId, """{"q1":"hello"}""");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SaveAfterTheWindowClosesFails()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Instance Closed Report", UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "08");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-30), 0, Now.AddDays(-1), Now.AddDays(-10));

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        using var request = SaveRequest(windowId, muqamId, """{"q1":"hello"}""");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SaveOnALockedInstanceFails()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Instance Locked Report", UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "09");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        using var first = SaveRequest(windowId, muqamId, """{"q1":"hello"}""");
        var firstResult = await (await client.SendAsync(first)).Content.ReadFromJsonAsync<SaveReportDraftResult>();
        await MarkInstanceSubmittedAsync(windowId, muqamId);

        using var second = SaveRequest(firstResult!.Id, muqamId, """{"q1":"too late"}""", firstResult.RowVersion);
        var secondResponse = await client.SendAsync(second);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task SaveWithNoPublishedTemplateFails()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Instance No Template Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "10");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        using var request = SaveRequest(windowId, muqamId, """{"q1":"hello"}""");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetForAnUnknownIdReturnsNotFound()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var muqamId = await CreateMuqamUnitAsync(secretariat, "12");

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        var response = await client.GetAsync(new Uri($"/api/v1/reports/{Guid.NewGuid()}?unit={muqamId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SecondSaveReusingTheOriginalWindowIdStillFindsTheExistingInstance()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Instance Window Id Reuse Report", UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "13");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        using var first = SaveRequest(windowId, muqamId, """{"q1":"hello"}""");
        var firstResult = await (await client.SendAsync(first)).Content.ReadFromJsonAsync<SaveReportDraftResult>();

        // A client that never learned the real instance id (e.g. it only ever attached a file,
        // never called Save itself) keeps using the window id — this must find the instance
        // ALREADY created for that (window, unit) pair rather than trying to create a second one
        // and violating the one-instance-per-(window,unit) unique index.
        using var second = SaveRequest(windowId, muqamId, """{"q1":"still the same instance"}""", firstResult!.RowVersion);
        var secondResponse = await client.SendAsync(second);
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<SaveReportDraftResult>();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(firstResult.Id, secondResult!.Id);

        var getResponse = await client.GetAsync(new Uri($"/api/v1/reports/{windowId}?unit={muqamId}", UriKind.Relative));
        var report = await getResponse.Content.ReadFromJsonAsync<ReportInstanceForEditing>();

        Assert.Equal(firstResult.Id, report!.Id);
    }

    [Fact]
    public async Task ADelegateCanAlsoSaveADraft()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Instance Delegate Report", UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "11");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.Delegate, DelegateScope.DraftOnly);

        using var request = SaveRequest(windowId, muqamId, """{"q1":"hello"}""");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
