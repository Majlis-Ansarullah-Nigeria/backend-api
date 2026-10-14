using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.History;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Domain.Access;
using HRS.Domain.History;
using HRS.Domain.Notifications;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

/// <summary>FR-HIS-004's background job logic, called directly — the same "call the processor,
/// skip the real Hangfire server" pattern OutboxDispatcherTests/WindowJobRunnerTests use.</summary>
[Collection(ApiTestCollection.Name)]
public class BulkExportProcessorTests(ApiFactory factory)
{
    private const string PublicUrl = "http://localhost:3000";

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

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Bulk01";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private static async Task<Guid> CreateReportTypeAsync(HttpClient client, string name)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MonthlyReport(name)))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        return created!.Id;
    }

    private static async Task PublishTemplateAsync(HttpClient client, Guid reportTypeId, string fieldKey, string fieldLabel)
    {
        var field = new TemplateField(
            fieldKey, TemplateFieldType.ShortText, fieldLabel, null, false, null, null, null, null, null, false,
            null, null, null, AggregationRule.None, null, null, null);
        var schema = new TemplateSchema([new TemplateSection("section_1", "Figures", [field])]);

        var draft = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(schema)))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);
    }

    // "BE"-prefixed codes — every test in HRS.Api.IntegrationTests shares one Testcontainers
    // Postgres, so unit codes must be namespaced per file to avoid (ParentId, Code) collisions.
    private static async Task<Guid> CreateMuqamAsync(HttpClient client, string suffix)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"BEZ{suffix}", $"Bulk Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"BED{suffix}", $"Bulk Dila {suffix}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var muqam = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"BEM{suffix}", $"Bulk Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dila!.Id, null, null, null, null, null)))
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

    private async Task<Guid> CreateSubmittedInstanceAsync(Guid windowId, Guid unitId, Guid submittedByUserId, string answersJson)
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
        instance.Submit(isLate: false, $"REF-BULK-{Guid.NewGuid():N}", isNil: false, Now, submittedByUserId);
        db.ReportInstances.Add(instance);
        await db.SaveChangesAsync();

        return instance.Id;
    }

    private async Task<BulkExport> CreateAndRunExportAsync(Guid requestedByUserId, IReadOnlyList<Guid> instanceIds, BulkExportFormat format)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var pdfRenderer = scope.ServiceProvider.GetRequiredService<IPdfRenderer>();
        var excelRenderer = scope.ServiceProvider.GetRequiredService<IExcelRenderer>();

        var scopeJson = JsonSerializer.Serialize(new InstanceIdsScope(instanceIds));
        var export = BulkExport.Create(
            Guid.CreateVersion7(), requestedByUserId, format, BulkExportScopeKind.InstanceIds, scopeJson, clock.UtcNow);
        db.BulkExports.Add(export);
        await db.SaveChangesAsync();

        await BulkExportProcessor.RunAsync(db, factory.FileStorage, pdfRenderer, excelRenderer, clock, PublicUrl, export.Id, CancellationToken.None);

        return await db.BulkExports.AsNoTracking().FirstAsync(e => e.Id == export.Id);
    }

    [Fact]
    public async Task ZipOfPdfsProducesAValidZipWithOneEntryPerReport()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Bulk Zip Report");
        await PublishTemplateAsync(secretariat, reportTypeId, "notes", "Notes");
        var muqamId = await CreateMuqamAsync(secretariat, "01");
        var window1 = await CreateWindowAsync(secretariat, reportTypeId, "Sep 2026");
        var window2 = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026");

        var headId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Bulk02");
        await factory.CreateRoleAssignmentAsync(headId, muqamId, RoleName.UnitHead);
        var instance1 = await CreateSubmittedInstanceAsync(window1, muqamId, headId, """{"notes":"first"}""");
        var instance2 = await CreateSubmittedInstanceAsync(window2, muqamId, headId, """{"notes":"second"}""");

        var export = await CreateAndRunExportAsync(headId, [instance1, instance2], BulkExportFormat.ZipOfPdfs);

        Assert.Equal(BulkExportStatus.Ready, export.Status);
        Assert.Equal(2, export.ReportCount);
        Assert.NotNull(export.StorageKey);
        Assert.True(factory.FileStorage.Contains(export.StorageKey!));

        var stored = await factory.FileStorage.GetAsync(export.StorageKey!, CancellationToken.None);
        using var zip = new ZipArchive(stored!.Content, ZipArchiveMode.Read);
        Assert.Equal(2, zip.Entries.Count);
        Assert.All(zip.Entries, e => Assert.EndsWith(".pdf", e.Name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExcelWorkbookUnionsColumnsAcrossDifferentReportTypes()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var typeA = await CreateReportTypeAsync(secretariat, "Bulk Excel Type A");
        var typeB = await CreateReportTypeAsync(secretariat, "Bulk Excel Type B");
        await PublishTemplateAsync(secretariat, typeA, "attendance", "Attendance");
        await PublishTemplateAsync(secretariat, typeB, "income", "Income");
        var muqamId = await CreateMuqamAsync(secretariat, "02");
        var windowA = await CreateWindowAsync(secretariat, typeA, "Oct 2026");
        var windowB = await CreateWindowAsync(secretariat, typeB, "Oct 2026");

        var headId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Bulk03");
        await factory.CreateRoleAssignmentAsync(headId, muqamId, RoleName.UnitHead);
        var instanceA = await CreateSubmittedInstanceAsync(windowA, muqamId, headId, """{"attendance":"42"}""");
        var instanceB = await CreateSubmittedInstanceAsync(windowB, muqamId, headId, """{"income":"1000"}""");

        var export = await CreateAndRunExportAsync(headId, [instanceA, instanceB], BulkExportFormat.ExcelWorkbook);

        Assert.Equal(BulkExportStatus.Ready, export.Status);
        Assert.Equal(2, export.ReportCount);

        var stored = await factory.FileStorage.GetAsync(export.StorageKey!, CancellationToken.None);
        using var workbook = new XLWorkbook(stored!.Content);
        var sheet = workbook.Worksheet("Reports");
        var headerRow = sheet.Row(1).CellsUsed().Select(c => c.GetString()).ToList();

        Assert.Contains("Attendance", headerRow);
        Assert.Contains("Income", headerRow);

        var allValues = sheet.CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("42", allValues);
        Assert.Contains("1000", allValues);
    }

    [Fact]
    public async Task InaccessibleInstanceIsSkippedBestEffortRatherThanFailingTheWholeExport()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Bulk Skip Report");
        await PublishTemplateAsync(secretariat, reportTypeId, "notes", "Notes");
        var ownMuqamId = await CreateMuqamAsync(secretariat, "03");
        var otherMuqamId = await CreateMuqamAsync(secretariat, "03b");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026");

        var headId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Bulk04");
        await factory.CreateRoleAssignmentAsync(headId, ownMuqamId, RoleName.UnitHead);
        var ownInstance = await CreateSubmittedInstanceAsync(windowId, ownMuqamId, headId, """{"notes":"mine"}""");

        var otherHeadId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Bulk05");
        await factory.CreateRoleAssignmentAsync(otherHeadId, otherMuqamId, RoleName.UnitHead);
        var inaccessibleInstance = await CreateSubmittedInstanceAsync(windowId, otherMuqamId, otherHeadId, """{"notes":"not mine"}""");

        var export = await CreateAndRunExportAsync(headId, [ownInstance, inaccessibleInstance], BulkExportFormat.ZipOfPdfs);

        Assert.Equal(BulkExportStatus.Ready, export.Status);
        Assert.Equal(1, export.ReportCount);
    }

    [Fact]
    public async Task ExportWithNoResolvableReportsIsMarkedFailed()
    {
        var headId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Bulk06");

        var export = await CreateAndRunExportAsync(headId, [Guid.CreateVersion7()], BulkExportFormat.ZipOfPdfs);

        Assert.Equal(BulkExportStatus.Failed, export.Status);
        Assert.NotNull(export.ErrorMessage);
    }

    [Fact]
    public async Task SuccessfulExportEnqueuesAnExportReadyNotificationForTheRequester()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Bulk Notify Report");
        await PublishTemplateAsync(secretariat, reportTypeId, "notes", "Notes");
        var muqamId = await CreateMuqamAsync(secretariat, "04");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026");

        var headId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Bulk07");
        await factory.CreateRoleAssignmentAsync(headId, muqamId, RoleName.UnitHead);
        var instanceId = await CreateSubmittedInstanceAsync(windowId, muqamId, headId, """{"notes":"x"}""");

        await CreateAndRunExportAsync(headId, [instanceId], BulkExportFormat.ZipOfPdfs);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = await db.OutboxMessages.AsNoTracking()
            .SingleAsync(m => m.RecipientUserId == headId && m.EventType == NotificationEventType.ExportReady);
        Assert.Contains("/exports/", message.PayloadJson);
    }
}
