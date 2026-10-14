using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Organization.Units;
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

/// <summary>FR-HIS-003: the branded single-report PDF and Excel downloads.</summary>
[Collection(ApiTestCollection.Name)]
public class HistoryReportDownloadTests(ApiFactory factory)
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
        const string password = "Correct-Horse-Down1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedUserAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Down2";
        var userId = await factory.CreateUserAsync(email, password);

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

    /// <summary>One plain field and one Confidential field, one section.</summary>
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

    // "HD"-prefixed codes — every test in HRS.Api.IntegrationTests shares one Testcontainers
    // Postgres, so unit codes must be namespaced per file to avoid (ParentId, Code) collisions.
    private static async Task<Guid> CreateZoneAsync(HttpClient client, string suffix)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"HDZ{suffix}", $"Download Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        return zone!.Id;
    }

    private static async Task<Guid> CreateDilaAsync(HttpClient client, Guid zoneId, string suffix)
    {
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"HDD{suffix}", $"Download Dila {suffix}", OrganizationSeedIds.DilaLevelId, zoneId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        return dila!.Id;
    }

    private static async Task<Guid> CreateMuqamAsync(HttpClient client, Guid dilaId, string suffix)
    {
        var muqam = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"HDM{suffix}", $"Download Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dilaId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        return muqam!.Id;
    }

    private static async Task<(Guid DilaId, Guid MuqamId)> CreateDilaWithOneMuqamAsync(HttpClient client, string suffix)
    {
        var zoneId = await CreateZoneAsync(client, suffix);
        var dilaId = await CreateDilaAsync(client, zoneId, suffix);
        var muqamId = await CreateMuqamAsync(client, dilaId, suffix);
        return (dilaId, muqamId);
    }

    private static async Task<Guid> CreateWindowAsync(HttpClient client, Guid reportTypeId, string periodLabel)
    {
        var request = new CreateSubmissionWindowRequest(
            periodLabel, Now.AddDays(-10), 3, Now.AddDays(20), [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))]);
        var created = await (await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        return created!.Window.Id;
    }

    /// <summary>Writes a Submitted report instance directly, same shortcut HistoryEndpointsTests uses.</summary>
    private async Task<Guid> CreateSubmittedInstanceAsync(Guid windowId, Guid unitId, Guid? submittedByUserId, string answersJson = "{}")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var window = await db.SubmissionWindows.AsNoTracking().FirstAsync(w => w.Id == windowId);
        var templateVersion = await db.TemplateVersions
            .Where(t => t.ReportTypeId == window.ReportTypeId && t.Status == TemplateVersionStatus.Published)
            .FirstAsync();

        var instance = ReportInstance.Create(Guid.CreateVersion7(), windowId, unitId, templateVersion.Id, clock.UtcNow);
        instance.Save(answersJson);
        instance.Submit(isLate: false, $"REF-DOWN-{Guid.NewGuid():N}", isNil: false, Now, submittedByUserId);
        db.ReportInstances.Add(instance);
        await db.SaveChangesAsync();

        return instance.Id;
    }

    [Fact]
    public async Task HeadCanDownloadPdfAndExcelForTheirOwnSubmittedReport()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Download Own Report", observerDepth: 0);
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (_, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "01");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026");

        var (headClient, headUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(headUserId, muqamId, RoleName.UnitHead);
        var instanceId = await CreateSubmittedInstanceAsync(windowId, muqamId, headUserId, """{"notes":"all is well","welfare_case":"secret"}""");

        var pdfResponse = await headClient.GetAsync(new Uri($"/api/v1/history/{instanceId}/pdf", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal("application/pdf", pdfResponse.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await pdfResponse.Content.ReadAsByteArrayAsync());

        var excelResponse = await headClient.GetAsync(new Uri($"/api/v1/history/{instanceId}/excel", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, excelResponse.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            excelResponse.Content.Headers.ContentType?.MediaType);

        using var workbook = new XLWorkbook(await excelResponse.Content.ReadAsStreamAsync());
        var sheet = workbook.Worksheet("Report");
        var cellValues = sheet.CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("secret", cellValues);
    }

    [Fact]
    public async Task ObserverDownloadingAnObservableReportGetsConfidentialFieldsMasked()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Download Observed Report", observerDepth: 1);
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "02");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026");

        var muqamHeadId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Down3");
        await factory.CreateRoleAssignmentAsync(muqamHeadId, muqamId, RoleName.UnitHead);
        var instanceId = await CreateSubmittedInstanceAsync(windowId, muqamId, muqamHeadId, """{"notes":"all is well","welfare_case":"secret"}""");

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var excelResponse = await dilaClient.GetAsync(new Uri($"/api/v1/history/{instanceId}/excel", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, excelResponse.StatusCode);

        using var workbook = new XLWorkbook(await excelResponse.Content.ReadAsStreamAsync());
        var sheet = workbook.Worksheet("Report");
        var cellValues = sheet.CellsUsed().Select(c => c.GetString()).ToList();
        Assert.DoesNotContain("secret", cellValues);
        Assert.Contains("Hidden from observers", cellValues);
    }

    [Fact]
    public async Task ObserverIsForbiddenFromDownloadingANonObservableReportType()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Download Hidden Report", observerDepth: 0);
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "03");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026");

        var muqamHeadId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Down4");
        await factory.CreateRoleAssignmentAsync(muqamHeadId, muqamId, RoleName.UnitHead);
        var instanceId = await CreateSubmittedInstanceAsync(windowId, muqamId, muqamHeadId);

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var response = await dilaClient.GetAsync(new Uri($"/api/v1/history/{instanceId}/pdf", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DownloadingANotYetSubmittedReportFails()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Download Not Submitted Report", observerDepth: 0);
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (_, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "04");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026");

        var (headClient, headUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(headUserId, muqamId, RoleName.UnitHead);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var templateVersion = await db.TemplateVersions
            .Where(t => t.ReportTypeId == reportTypeId && t.Status == TemplateVersionStatus.Published)
            .FirstAsync();
        var draft = ReportInstance.Create(Guid.CreateVersion7(), windowId, muqamId, templateVersion.Id, clock.UtcNow);
        db.ReportInstances.Add(draft);
        await db.SaveChangesAsync();

        var response = await headClient.GetAsync(new Uri($"/api/v1/history/{draft.Id}/pdf", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DownloadingAnUnknownInstanceIdReturnsNotFound()
    {
        var (client, _) = await CreateAuthenticatedUserAsync();

        var response = await client.GetAsync(new Uri($"/api/v1/history/{Guid.CreateVersion7()}/pdf", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
