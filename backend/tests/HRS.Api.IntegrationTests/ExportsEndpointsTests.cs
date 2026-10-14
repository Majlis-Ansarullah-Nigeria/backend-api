using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.History;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Domain.Access;
using HRS.Domain.History;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

/// <summary>FR-HIS-004: the export composer endpoints — the own-selection bulk export
/// (/history/bulk-export), the Secretariat-only organization-wide export
/// (/secretariat/exports), and the shared status/list/download endpoints under /exports.</summary>
[Collection(ApiTestCollection.Name)]
public class ExportsEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CreateReportTypeRequest MonthlyReport(string name) => new(
        name,
        "Monthly activity figures",
        ReportTypeCategory.GeneralMonthly,
        ReportTypeFrequency.Monthly,
        [UnitLevelCode.Muqam],
        UnitLevelCode.National,
        ObserverDepth: 0,
        AllowAttachments: true, AllowNilReport: false);

    private async Task<(HttpClient Client, Guid UserId)> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Exp01";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, userId);
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedUserAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Exp02";
        var userId = await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, userId);
    }

    private static async Task<Guid> CreateReportTypeAsync(HttpClient client, string name)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MonthlyReport(name)))
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

    // "EX"-prefixed codes — every test in HRS.Api.IntegrationTests shares one Testcontainers
    // Postgres, so unit codes must be namespaced per file to avoid (ParentId, Code) collisions.
    private static async Task<Guid> CreateMuqamAsync(HttpClient client, string suffix)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"EXZ{suffix}", $"Export Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"EXD{suffix}", $"Export Dila {suffix}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var muqam = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"EXM{suffix}", $"Export Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dila!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        return muqam!.Id;
    }

    private static async Task<Guid> CreateWindowAsync(HttpClient client, Guid reportTypeId, string periodLabel)
    {
        var request = new CreateSubmissionWindowRequest(
            periodLabel, Now.AddDays(-10), 3, Now.AddDays(20), [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))]);
        var created = await (await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        return created!.Window.Id;
    }

    private async Task<Guid> CreateSubmittedInstanceAsync(Guid windowId, Guid unitId, Guid submittedByUserId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var window = await db.SubmissionWindows.AsNoTracking().FirstAsync(w => w.Id == windowId);
        var templateVersion = await db.TemplateVersions
            .Where(t => t.ReportTypeId == window.ReportTypeId && t.Status == TemplateVersionStatus.Published)
            .FirstAsync();

        var instance = ReportInstance.Create(Guid.CreateVersion7(), windowId, unitId, templateVersion.Id, clock.UtcNow);
        instance.Save("{}");
        instance.Submit(isLate: false, $"REF-EXP-{Guid.NewGuid():N}", isNil: false, Now, submittedByUserId);
        db.ReportInstances.Add(instance);
        await db.SaveChangesAsync();

        return instance.Id;
    }

    [Fact]
    public async Task CreatingABulkExportEnqueuesAJobAndReturnsAPendingSummary()
    {
        var (secretariat, _) = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Export Bulk Endpoint Report");
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamAsync(secretariat, "01");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026");

        var (headClient, headUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(headUserId, muqamId, RoleName.UnitHead);
        var instanceId = await CreateSubmittedInstanceAsync(windowId, muqamId, headUserId);

        var response = await headClient.PostAsJsonAsync(
            "/api/v1/history/bulk-export", new CreateBulkExportRequest([instanceId], BulkExportFormat.ZipOfPdfs));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<ExportSummary>();
        Assert.Equal(BulkExportStatus.Pending, summary!.Status);
        Assert.True(factory.JobScheduler.WasEnqueued(summary.Id));
    }

    [Fact]
    public async Task CreatingABulkExportWithNoInstancesFails()
    {
        var (headClient, _) = await CreateAuthenticatedUserAsync();

        var response = await headClient.PostAsJsonAsync(
            "/api/v1/history/bulk-export", new CreateBulkExportRequest([], BulkExportFormat.ZipOfPdfs));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SecretariatCanCreateAnOrganizationWideExport()
    {
        var (secretariat, secretariatUserId) = await CreateSecretariatClientAsync();

        var response = await secretariat.PostAsJsonAsync(
            "/api/v1/secretariat/exports", new CreateOrganizationExportRequest(null, null, null, null, BulkExportFormat.ExcelWorkbook));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<ExportSummary>();
        Assert.Equal(secretariatUserId, summary!.RequestedByUserId);
        Assert.True(factory.JobScheduler.WasEnqueued(summary.Id));
    }

    [Fact]
    public async Task NonSecretariatIsForbiddenFromCreatingAnOrganizationWideExport()
    {
        var (headClient, _) = await CreateAuthenticatedUserAsync();

        var response = await headClient.PostAsJsonAsync(
            "/api/v1/secretariat/exports", new CreateOrganizationExportRequest(null, null, null, null, BulkExportFormat.ExcelWorkbook));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanSeeTheirOwnExportButAnotherHeadCannot()
    {
        var (headClient, headUserId) = await CreateAuthenticatedUserAsync();
        var created = await (await headClient.PostAsJsonAsync(
                "/api/v1/history/bulk-export", new CreateBulkExportRequest([Guid.CreateVersion7()], BulkExportFormat.ZipOfPdfs)))
            .Content.ReadFromJsonAsync<ExportSummary>();

        var ownResponse = await headClient.GetAsync(new Uri($"/api/v1/exports/{created!.Id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);

        var (otherClient, _) = await CreateAuthenticatedUserAsync();
        var otherResponse = await otherClient.GetAsync(new Uri($"/api/v1/exports/{created.Id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Forbidden, otherResponse.StatusCode);
    }

    [Fact]
    public async Task SecretariatCanSeeAnyUsersExport()
    {
        var (headClient, _) = await CreateAuthenticatedUserAsync();
        var created = await (await headClient.PostAsJsonAsync(
                "/api/v1/history/bulk-export", new CreateBulkExportRequest([Guid.CreateVersion7()], BulkExportFormat.ZipOfPdfs)))
            .Content.ReadFromJsonAsync<ExportSummary>();

        var (secretariat, _) = await CreateSecretariatClientAsync();
        var response = await secretariat.GetAsync(new Uri($"/api/v1/exports/{created!.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MyExportsListsOnlyTheCallersOwnExportsUnlessSecretariat()
    {
        var (headClient, headUserId) = await CreateAuthenticatedUserAsync();
        await headClient.PostAsJsonAsync("/api/v1/history/bulk-export", new CreateBulkExportRequest([Guid.CreateVersion7()], BulkExportFormat.ZipOfPdfs));

        var (otherClient, _) = await CreateAuthenticatedUserAsync();
        await otherClient.PostAsJsonAsync("/api/v1/history/bulk-export", new CreateBulkExportRequest([Guid.CreateVersion7()], BulkExportFormat.ZipOfPdfs));

        var ownList = await (await headClient.GetAsync(new Uri("/api/v1/exports", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<ExportSummary>>();
        Assert.All(ownList!, e => Assert.Equal(headUserId, e.RequestedByUserId));
    }

    [Fact]
    public async Task DownloadUrlIsRefusedUntilTheExportIsReady()
    {
        var (headClient, _) = await CreateAuthenticatedUserAsync();
        var created = await (await headClient.PostAsJsonAsync(
                "/api/v1/history/bulk-export", new CreateBulkExportRequest([Guid.CreateVersion7()], BulkExportFormat.ZipOfPdfs)))
            .Content.ReadFromJsonAsync<ExportSummary>();

        var response = await headClient.GetAsync(new Uri($"/api/v1/exports/{created!.Id}/download", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DownloadUrlSucceedsOnceTheExportIsReady()
    {
        using var secretariat = (await CreateSecretariatClientAsync()).Client;
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Export Download Ready Report");
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamAsync(secretariat, "02");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026");

        var (headClient, headUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(headUserId, muqamId, RoleName.UnitHead);
        var instanceId = await CreateSubmittedInstanceAsync(windowId, muqamId, headUserId);

        var created = await (await headClient.PostAsJsonAsync(
                "/api/v1/history/bulk-export", new CreateBulkExportRequest([instanceId], BulkExportFormat.ZipOfPdfs)))
            .Content.ReadFromJsonAsync<ExportSummary>();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var pdfRenderer = scope.ServiceProvider.GetRequiredService<IPdfRenderer>();
            var excelRenderer = scope.ServiceProvider.GetRequiredService<IExcelRenderer>();
            await BulkExportProcessor.RunAsync(
                db, factory.FileStorage, pdfRenderer, excelRenderer, clock, "http://localhost:3000", created!.Id, CancellationToken.None);
        }

        var response = await headClient.GetAsync(new Uri($"/api/v1/exports/{created!.Id}/download", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
