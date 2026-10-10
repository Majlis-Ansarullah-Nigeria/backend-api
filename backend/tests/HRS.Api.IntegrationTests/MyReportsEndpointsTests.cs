using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Submissions.MyReports;
using HRS.Application.Features.Templates.ReportTypes;
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
public class MyReportsEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CreateReportTypeRequest MonthlyReport(string name, params UnitLevelCode[] levels) => new(
        name,
        "Monthly activity figures",
        ReportTypeCategory.GeneralMonthly,
        ReportTypeFrequency.Monthly,
        levels,
        UnitLevelCode.National,
        1,
        AllowAttachments: true, AllowNilReport: false);

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedUserAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-MyRep1";
        var userId = await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, userId);
    }

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-MyRep2";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

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

    // "MR"-prefixed codes, not the plain Z/D/M or other files' prefixes — every test in
    // HRS.Api.IntegrationTests shares one Testcontainers Postgres, so unit codes must be
    // namespaced per file to avoid (ParentId, Code) collisions across files.
    private static async Task<Guid> CreateMuqamUnitAsync(HttpClient client, string suffix)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"MRZ{suffix}", $"MyReports Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"MRD{suffix}", $"MyReports Dila {suffix}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var muqam = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"MRM{suffix}", $"MyReports Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dila!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();

        return muqam!.Id;
    }

    private static async Task<Guid> CreateWindowAsync(
        HttpClient client,
        Guid reportTypeId,
        DateTimeOffset opensAt,
        int graceDays,
        DateTimeOffset closesAt,
        DateTimeOffset dueAt,
        IReadOnlyList<Guid>? targetUnitIds = null)
    {
        var request = new CreateSubmissionWindowRequest(
            "My Reports Test Window", opensAt, graceDays, closesAt, [new WindowDueDate(UnitLevelCode.Muqam, dueAt)], targetUnitIds);
        var created = await (await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        return created!.Window.Id;
    }

    /// <summary>Seeds a Draft ReportInstance directly — there's no Save endpoint yet (M5.3).</summary>
    private async Task SeedDraftInstanceAsync(Guid windowId, Guid unitId, Guid reportTypeId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var templateVersion = TemplateVersion.CreateDraft(Guid.CreateVersion7(), reportTypeId, 1, """{"sections":[]}""", Now);
        db.TemplateVersions.Add(templateVersion);

        var instance = ReportInstance.Create(Guid.CreateVersion7(), windowId, unitId, templateVersion.Id, Now);
        instance.Save("""{"q1":"a"}""");
        db.ReportInstances.Add(instance);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AUserWithNoRoleAssignmentsSeesAnEmptyList()
    {
        var (client, _) = await CreateAuthenticatedUserAsync();

        var response = await client.GetAsync(new Uri("/api/v1/me/reports", UriKind.Relative));
        var reports = await response.Content.ReadFromJsonAsync<List<MyReportSummary>>();

        Assert.Empty(reports!);
    }

    [Fact]
    public async Task AUnitHeadSeesAReportDueForTheirUnit()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "My Reports Head Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "01");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        var response = await client.GetAsync(new Uri("/api/v1/me/reports", UriKind.Relative));
        var reports = await response.Content.ReadFromJsonAsync<List<MyReportSummary>>();

        // The response is "my reports across every applicable report type," so a Muqam-level unit
        // also picks up every other Muqam-level window created by the rest of the shared-DB test
        // suite (see the file-level note on unit-code namespacing) — filter to this test's own
        // window rather than asserting on the full collection.
        var report = Assert.Single(reports!, r => r.WindowId == windowId);
        Assert.Equal(muqamId, report.UnitId);
        Assert.Equal(ReportLineStatus.NotStarted, report.Status);
        Assert.Null(report.InstanceId);
    }

    [Fact]
    public async Task ADelegateAlsoSeesReportsForTheirUnit()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "My Reports Delegate Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "02");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.Delegate, DelegateScope.DraftOnly);

        var response = await client.GetAsync(new Uri("/api/v1/me/reports", UriKind.Relative));
        var reports = await response.Content.ReadFromJsonAsync<List<MyReportSummary>>();

        Assert.Contains(reports!, r => r.WindowId == windowId);
    }

    [Fact]
    public async Task ReportsAcrossMultipleUnitsAreAllIncluded()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "My Reports Multi Unit Report", UnitLevelCode.Muqam);
        var muqamOneId = await CreateMuqamUnitAsync(secretariat, "03a");
        var muqamTwoId = await CreateMuqamUnitAsync(secretariat, "03b");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamOneId, RoleName.UnitHead);
        await factory.CreateRoleAssignmentAsync(userId, muqamTwoId, RoleName.UnitHead);

        var response = await client.GetAsync(new Uri("/api/v1/me/reports", UriKind.Relative));
        var reports = await response.Content.ReadFromJsonAsync<List<MyReportSummary>>();

        var forThisWindow = reports!.Where(r => r.WindowId == windowId).ToList();
        Assert.Equal(2, forThisWindow.Count);
        Assert.Contains(forThisWindow, r => r.UnitId == muqamOneId);
        Assert.Contains(forThisWindow, r => r.UnitId == muqamTwoId);
    }

    [Fact]
    public async Task AWindowTargetingOtherUnitsOnlyIsExcluded()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "My Reports Ad Hoc Report", UnitLevelCode.Muqam);
        var targetedMuqamId = await CreateMuqamUnitAsync(secretariat, "04a");
        var otherMuqamId = await CreateMuqamUnitAsync(secretariat, "04b");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10), [targetedMuqamId]);

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, otherMuqamId, RoleName.UnitHead);

        var response = await client.GetAsync(new Uri("/api/v1/me/reports", UriKind.Relative));
        var reports = await response.Content.ReadFromJsonAsync<List<MyReportSummary>>();

        Assert.DoesNotContain(reports!, r => r.WindowId == windowId);
    }

    [Fact]
    public async Task AnArchivedUnitIsExcluded()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "My Reports Archived Unit Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "05");
        await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);
        await secretariat.DeleteAsync(new Uri($"/api/v1/units/{muqamId}", UriKind.Relative));

        var response = await client.GetAsync(new Uri("/api/v1/me/reports", UriKind.Relative));
        var reports = await response.Content.ReadFromJsonAsync<List<MyReportSummary>>();

        Assert.Empty(reports!);
    }

    [Fact]
    public async Task AnExistingDraftInstanceIsReflectedInTheStatus()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "My Reports Draft Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "06");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));
        await SeedDraftInstanceAsync(windowId, muqamId, reportTypeId);

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        var response = await client.GetAsync(new Uri("/api/v1/me/reports", UriKind.Relative));
        var reports = await response.Content.ReadFromJsonAsync<List<MyReportSummary>>();

        var report = Assert.Single(reports!, r => r.WindowId == windowId);
        Assert.Equal(ReportLineStatus.Draft, report.Status);
        Assert.NotNull(report.InstanceId);
    }

    [Fact]
    public async Task ReportsAreSortedByDueDateAscending()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "My Reports Sort Report", UnitLevelCode.Muqam, UnitLevelCode.Dila);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "07");

        // Two windows for the same report type, different periods and due dates — the later-due
        // one created first, to prove the response is actually sorted and not just insertion order.
        var laterWindowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(20));
        var earlierWindowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(5));

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        var response = await client.GetAsync(new Uri("/api/v1/me/reports", UriKind.Relative));
        var reports = await response.Content.ReadFromJsonAsync<List<MyReportSummary>>();

        // The response as a whole is sorted ascending, so filtering to just these two windows
        // (out of however many the shared-DB suite's other tests also contributed) preserves
        // their relative order — the earlier-due window must still come first.
        var mine = reports!.Where(r => r.WindowId == laterWindowId || r.WindowId == earlierWindowId).ToList();
        Assert.Equal(2, mine.Count);
        Assert.Equal(earlierWindowId, mine[0].WindowId);
        Assert.Equal(laterWindowId, mine[1].WindowId);
    }

    [Fact]
    public async Task UnauthenticatedRequestsAreRejected()
    {
        using var client = factory.CreateAuthenticatedClient(handleCookies: false);

        var response = await client.GetAsync(new Uri("/api/v1/me/reports", UriKind.Relative));

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
