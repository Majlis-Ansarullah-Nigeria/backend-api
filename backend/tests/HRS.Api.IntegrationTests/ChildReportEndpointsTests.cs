using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Observer.ChildReport;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Submissions.ReportInstances;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class ChildReportEndpointsTests(ApiFactory factory)
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
        const string password = "Correct-Horse-Child1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedUserAsync(string name = "Test User")
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Child2";
        var userId = await factory.CreateUserAsync(email, password, name: name);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, userId);
    }

    private static async Task<Guid> CreateReportTypeAsync(HttpClient client, string name, int observerDepth)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MonthlyReport(name, observerDepth)))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        return created!.Id;
    }

    /// <summary>A plain "notes" field and a Confidential "welfare_case" field, one section.</summary>
    private static async Task PublishTemplateAsync(HttpClient client, Guid reportTypeId)
    {
        var notes = new TemplateField(
            "notes", TemplateFieldType.ShortText, "Notes", null, false, null, null, null, null, null, false,
            null, null, null, AggregationRule.None, null, null, null);
        var welfareCase = new TemplateField(
            "welfare_case", TemplateFieldType.ShortText, "Confidential details", null, false, null, null, null, null, null, true,
            null, null, null, AggregationRule.None, null, null, null);
        var schema = new TemplateSchema([new TemplateSection("section_1", "Welfare", [notes, welfareCase])]);

        var draft = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(schema)))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);
    }

    // "CR"-prefixed codes — every test in HRS.Api.IntegrationTests shares one Testcontainers
    // Postgres, so unit codes must be namespaced per file to avoid (ParentId, Code) collisions.
    private static async Task<(Guid DilaId, Guid MuqamId)> CreateDilaWithOneMuqamAsync(HttpClient client, string suffix)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"CRZ{suffix}", $"Child Report Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"CRD{suffix}", $"Child Report Dila {suffix}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var muqam = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"CRM{suffix}", $"Child Report Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dila!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();

        return (dila.Id, muqam!.Id);
    }

    private static async Task<Guid> CreateWindowAsync(HttpClient client, Guid reportTypeId)
    {
        var request = new CreateSubmissionWindowRequest(
            "Sep 2026", Now.AddDays(-10), 3, Now.AddDays(20), [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))]);
        var created = await (await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        return created!.Window.Id;
    }

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, Guid reportId, Guid unitId, Dictionary<string, object?> answers) =>
        client.PutAsJsonAsync($"/api/v1/reports/{reportId}?unit={unitId}", new SaveReportDraftRequest(answers));

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid reportId, Guid unitId) =>
        client.PostAsJsonAsync($"/api/v1/reports/{reportId}/submit?unit={unitId}", new SubmitReportRequest(Confirmed: true));

    /// <summary>A Dila (observer), one Muqam child (submitter), a report type/window/template, and
    /// the Muqam head's draft saved with both fields filled.</summary>
    private async Task<(Guid DilaId, Guid MuqamId, Guid ReportId, string SubmitterName)> SetUpSubmittedReportAsync(string suffix, int observerDepth = 1)
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, $"Child Report Type {suffix}", observerDepth);
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, suffix);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);

        const string submitterName = "Olu Falana";
        var (muqamClient, muqamUserId) = await CreateAuthenticatedUserAsync(submitterName);
        await factory.CreateRoleAssignmentAsync(muqamUserId, muqamId, RoleName.UnitHead);

        await SaveAsync(muqamClient, windowId, muqamId, new Dictionary<string, object?> { ["notes"] = "All quiet", ["welfare_case"] = "Family X needs support" });
        await SubmitAsync(muqamClient, windowId, muqamId);

        return (dilaId, muqamId, windowId, submitterName);
    }

    [Fact]
    public async Task DilaHeadSeesTheSubmittedReportWithConfidentialValueMasked()
    {
        var (dilaId, muqamId, reportId, submitterName) = await SetUpSubmittedReportAsync("01");

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var response = await dilaClient.GetAsync(new Uri($"/api/v1/units/{muqamId}/reports/{reportId}", UriKind.Relative));
        var view = await response.Content.ReadFromJsonAsync<ChildReportView>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(submitterName, view!.SubmittedByName);
        Assert.NotNull(view.SubmittedAt);

        var fields = view.Receipt.Sections.SelectMany(s => s.Fields).ToList();
        Assert.Contains(fields, f => f.Label == "Notes" && f.Value == "All quiet" && !f.IsMasked);
        Assert.Contains(fields, f => f.Label == "Confidential details" && f.Value == "Hidden from observers" && f.IsMasked);
    }

    [Fact]
    public async Task DownloadPdfAlsoMasksConfidentialValues()
    {
        var (dilaId, muqamId, reportId, _) = await SetUpSubmittedReportAsync("02");

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var response = await dilaClient.GetAsync(new Uri($"/api/v1/units/{muqamId}/reports/{reportId}/pdf", UriKind.Relative));
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task DraftReportIsNotObservable()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Child Report Draft Type", observerDepth: 1);
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "03");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);

        var (muqamClient, muqamUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(muqamUserId, muqamId, RoleName.UnitHead);
        await SaveAsync(muqamClient, windowId, muqamId, new Dictionary<string, object?> { ["notes"] = "Still drafting" });

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var response = await dilaClient.GetAsync(new Uri($"/api/v1/units/{muqamId}/reports/{windowId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UnrelatedUserCannotObserveTheReport()
    {
        var (_, muqamId, reportId, _) = await SetUpSubmittedReportAsync("04");

        var (client, _) = await CreateAuthenticatedUserAsync();

        var response = await client.GetAsync(new Uri($"/api/v1/units/{muqamId}/reports/{reportId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ADelegateOnTheParentCannotObserveTheReportOnlyTheHeadCan()
    {
        var (dilaId, muqamId, reportId, _) = await SetUpSubmittedReportAsync("05");

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, dilaId, RoleName.Delegate, DelegateScope.DraftOnly);

        var response = await client.GetAsync(new Uri($"/api/v1/units/{muqamId}/reports/{reportId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReportTypeWithObserverDepthZeroIsNotObservableEvenByTheParentHead()
    {
        var (dilaId, muqamId, reportId, _) = await SetUpSubmittedReportAsync("06", observerDepth: 0);

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var response = await dilaClient.GetAsync(new Uri($"/api/v1/units/{muqamId}/reports/{reportId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
