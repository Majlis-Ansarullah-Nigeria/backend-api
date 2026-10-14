using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.History;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Submissions.MyReports;
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

/// <summary>FR-HIS-001/002: the scoped history list, its filters and full-text search.</summary>
[Collection(ApiTestCollection.Name)]
public class HistoryEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CreateReportTypeRequest MonthlyReport(string name, int observerDepth, params UnitLevelCode[] levels) => new(
        name,
        "Monthly activity figures",
        ReportTypeCategory.GeneralMonthly,
        ReportTypeFrequency.Monthly,
        levels,
        UnitLevelCode.National,
        observerDepth,
        AllowAttachments: true, AllowNilReport: false);

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Hist1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedUserAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Hist2";
        var userId = await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, userId);
    }

    private static async Task<Guid> CreateReportTypeAsync(HttpClient client, string name, int observerDepth, params UnitLevelCode[] levels)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MonthlyReport(name, observerDepth, levels)))
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

    // "HI"-prefixed codes — every test in HRS.Api.IntegrationTests shares one Testcontainers
    // Postgres, so unit codes must be namespaced per file to avoid (ParentId, Code) collisions.
    private static async Task<Guid> CreateZoneAsync(HttpClient client, string suffix)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"HIZ{suffix}", $"History Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        return zone!.Id;
    }

    private static async Task<Guid> CreateDilaAsync(HttpClient client, Guid zoneId, string suffix)
    {
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"HID{suffix}", $"History Dila {suffix}", OrganizationSeedIds.DilaLevelId, zoneId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        return dila!.Id;
    }

    private static async Task<Guid> CreateMuqamAsync(HttpClient client, Guid dilaId, string suffix)
    {
        var muqam = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"HIM{suffix}", $"History Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dilaId, null, null, null, null, null)))
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

    private static async Task<Guid> CreateWindowAsync(
        HttpClient client, Guid reportTypeId, string periodLabel, DateTimeOffset opensAt, int graceDays, DateTimeOffset closesAt, DateTimeOffset dueAt)
    {
        var request = new CreateSubmissionWindowRequest(
            periodLabel, opensAt, graceDays, closesAt, [new WindowDueDate(UnitLevelCode.Muqam, dueAt)]);
        var created = await (await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        return created!.Window.Id;
    }

    /// <summary>Writes a Submitted report instance directly (bypassing the save/submit HTTP
    /// endpoints, same shortcut ComplianceGridEndpointsTests uses) so tests can control
    /// AnswersJson and SubmittedByUserId precisely.</summary>
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
        instance.Submit(isLate: false, $"REF-HIST-{Guid.NewGuid():N}", isNil: false, Now, submittedByUserId);
        db.ReportInstances.Add(instance);
        await db.SaveChangesAsync();

        return instance.Id;
    }

    private static async Task<HistoryPage> GetHistoryAsync(HttpClient client, string query = "") =>
        (await (await client.GetAsync(new Uri($"/api/v1/history{query}", UriKind.Relative))).Content.ReadFromJsonAsync<HistoryPage>())!;

    [Fact]
    public async Task HeadSeesTheirOwnPastSubmittedReportLabeledYou()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "History Own Report", observerDepth: 0, UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var (_, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "01");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026", Now.AddDays(-10), 3, Now.AddDays(20), Now.AddDays(10));

        var (headClient, headUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(headUserId, muqamId, RoleName.UnitHead);
        await CreateSubmittedInstanceAsync(windowId, muqamId, headUserId);

        var page = await GetHistoryAsync(headClient);

        var row = Assert.Single(page.Rows);
        Assert.Equal(muqamId, row.UnitId);
        Assert.Equal(ReportLineStatus.Submitted, row.Status);
        Assert.Equal("You", row.SubmittedByLabel);
        Assert.False(row.IsObserved);
    }

    [Fact]
    public async Task ObserverSeesTheChildsReportWithTheCurrentHeadsRealName()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "History Observed Report", observerDepth: 1, UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "02");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026", Now.AddDays(-10), 3, Now.AddDays(20), Now.AddDays(10));

        var muqamHeadId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Hist3", name: "Amina Bello");
        await factory.CreateRoleAssignmentAsync(muqamHeadId, muqamId, RoleName.UnitHead);
        await CreateSubmittedInstanceAsync(windowId, muqamId, muqamHeadId);

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var page = await GetHistoryAsync(dilaClient);

        var row = Assert.Single(page.Rows);
        Assert.Equal(muqamId, row.UnitId);
        Assert.Equal("Amina Bello", row.SubmittedByLabel);
        Assert.True(row.IsObserved);
    }

    [Fact]
    public async Task AReportSubmittedByAFormerHeadIsLabeledPreviousHead()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "History Previous Head Report", observerDepth: 0, UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var (_, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "03");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026", Now.AddDays(-10), 3, Now.AddDays(20), Now.AddDays(10));

        var formerHeadId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Hist4");
        await factory.CreateRoleAssignmentAsync(formerHeadId, muqamId, RoleName.UnitHead, startsAt: Now.AddDays(-60), endsAt: Now.AddDays(-5));
        await CreateSubmittedInstanceAsync(windowId, muqamId, formerHeadId);

        var (currentHeadClient, currentHeadUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(currentHeadUserId, muqamId, RoleName.UnitHead, startsAt: Now.AddDays(-4));

        var page = await GetHistoryAsync(currentHeadClient);

        var row = Assert.Single(page.Rows);
        Assert.Equal("Previous head", row.SubmittedByLabel);
    }

    [Fact]
    public async Task NonObservableReportTypeNeverAppearsForAnObserver()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "History Hidden Report", observerDepth: 0, UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "04");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026", Now.AddDays(-10), 3, Now.AddDays(20), Now.AddDays(10));

        var muqamHeadId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Hist5");
        await factory.CreateRoleAssignmentAsync(muqamHeadId, muqamId, RoleName.UnitHead);
        await CreateSubmittedInstanceAsync(windowId, muqamId, muqamHeadId);

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var page = await GetHistoryAsync(dilaClient);

        Assert.Empty(page.Rows);
    }

    [Fact]
    public async Task FilteringByStatusNarrowsResults()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "History Status Filter Report", observerDepth: 0, UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var (_, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "05");
        var window1 = await CreateWindowAsync(secretariat, reportTypeId, "Aug 2026", Now.AddDays(-70), 3, Now.AddDays(-40), Now.AddDays(-50));
        var window2 = await CreateWindowAsync(secretariat, reportTypeId, "Sep 2026", Now.AddDays(-10), 3, Now.AddDays(20), Now.AddDays(10));

        var (headClient, headUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(headUserId, muqamId, RoleName.UnitHead);
        var acknowledgedId = await CreateSubmittedInstanceAsync(window1, muqamId, headUserId);
        await CreateSubmittedInstanceAsync(window2, muqamId, headUserId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var instance = await db.ReportInstances.FirstAsync(i => i.Id == acknowledgedId);
            instance.Open();
            instance.Acknowledge(clock.UtcNow);
            await db.SaveChangesAsync();
        }

        var page = await GetHistoryAsync(headClient, $"?status={(int)ReportLineStatus.Acknowledged}");

        var row = Assert.Single(page.Rows);
        Assert.Equal(acknowledgedId, row.InstanceId);
    }

    [Fact]
    public async Task FilteringByReportTypeNarrowsResults()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var typeA = await CreateReportTypeAsync(secretariat, "History Type A Report", observerDepth: 0, UnitLevelCode.Muqam);
        var typeB = await CreateReportTypeAsync(secretariat, "History Type B Report", observerDepth: 0, UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, typeA);
        await PublishEmptyTemplateAsync(secretariat, typeB);
        var (_, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "06");
        var windowA = await CreateWindowAsync(secretariat, typeA, "Oct 2026", Now.AddDays(-10), 3, Now.AddDays(20), Now.AddDays(10));
        var windowB = await CreateWindowAsync(secretariat, typeB, "Oct 2026", Now.AddDays(-10), 3, Now.AddDays(20), Now.AddDays(10));

        var (headClient, headUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(headUserId, muqamId, RoleName.UnitHead);
        var instanceA = await CreateSubmittedInstanceAsync(windowA, muqamId, headUserId);
        await CreateSubmittedInstanceAsync(windowB, muqamId, headUserId);

        var page = await GetHistoryAsync(headClient, $"?reportTypeId={typeA}");

        var row = Assert.Single(page.Rows);
        Assert.Equal(instanceA, row.InstanceId);
    }

    [Fact]
    public async Task FilteringByPeriodRangeNarrowsResults()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "History Period Filter Report", observerDepth: 0, UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var (_, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "07");
        var oldWindow = await CreateWindowAsync(secretariat, reportTypeId, "Jan 2026", Now.AddDays(-280), 3, Now.AddDays(-250), Now.AddDays(-260));
        var midWindow = await CreateWindowAsync(secretariat, reportTypeId, "Jun 2026", Now.AddDays(-130), 3, Now.AddDays(-100), Now.AddDays(-110));
        var recentWindow = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026", Now.AddDays(-10), 3, Now.AddDays(20), Now.AddDays(10));

        var (headClient, headUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(headUserId, muqamId, RoleName.UnitHead);
        await CreateSubmittedInstanceAsync(oldWindow, muqamId, headUserId);
        var midInstanceId = await CreateSubmittedInstanceAsync(midWindow, muqamId, headUserId);
        await CreateSubmittedInstanceAsync(recentWindow, muqamId, headUserId);

        var page = await GetHistoryAsync(headClient, $"?fromWindowId={midWindow}&toWindowId={midWindow}");

        var row = Assert.Single(page.Rows);
        Assert.Equal(midInstanceId, row.InstanceId);
    }

    [Fact]
    public async Task SearchFindsAMatchingAnswerScopedToTheCallersOwnUnits()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "History Search Report", observerDepth: 0, UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var (_, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "08");
        var (_, otherMuqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "08b");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026", Now.AddDays(-10), 3, Now.AddDays(20), Now.AddDays(10));

        var (headClient, headUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(headUserId, muqamId, RoleName.UnitHead);
        var matchingId = await CreateSubmittedInstanceAsync(
            windowId, muqamId, headUserId, """{"notes":"borehole repair needed urgently"}""");

        // Another unit's matching report must never leak into this caller's search results.
        var otherHeadId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Hist6");
        await factory.CreateRoleAssignmentAsync(otherHeadId, otherMuqamId, RoleName.UnitHead);
        await CreateSubmittedInstanceAsync(windowId, otherMuqamId, otherHeadId, """{"notes":"borehole repair also needed here"}""");

        var page = await GetHistoryAsync(headClient, "?search=borehole");

        var row = Assert.Single(page.Rows);
        Assert.Equal(matchingId, row.InstanceId);
    }

    [Fact]
    public async Task PaginationReturnsTheRequestedPageAndTotalCount()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "History Pagination Report", observerDepth: 0, UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var (_, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "09");

        var (headClient, headUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(headUserId, muqamId, RoleName.UnitHead);

        for (var i = 0; i < 3; i++)
        {
            var windowId = await CreateWindowAsync(
                secretariat, reportTypeId, $"Period {i}", Now.AddDays(-10 - i * 40), 3, Now.AddDays(20 - i * 40), Now.AddDays(10 - i * 40));
            await CreateSubmittedInstanceAsync(windowId, muqamId, headUserId);
        }

        var page = await GetHistoryAsync(headClient, "?page=1&pageSize=2");

        Assert.Equal(2, page.Rows.Count);
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task GetHistoryFiltersReturnsOnlyTheCallersOwnScopedReportTypesAndPeriods()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var ownTypeId = await CreateReportTypeAsync(secretariat, "History Filters Own Report", observerDepth: 0, UnitLevelCode.Muqam);
        var unrelatedTypeId = await CreateReportTypeAsync(secretariat, "History Filters Unrelated Report", observerDepth: 0, UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, ownTypeId);
        await PublishEmptyTemplateAsync(secretariat, unrelatedTypeId);
        var (_, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "10");
        var (_, otherMuqamId) = await CreateDilaWithOneMuqamAsync(secretariat, "10b");
        var windowId = await CreateWindowAsync(secretariat, ownTypeId, "Oct 2026", Now.AddDays(-10), 3, Now.AddDays(20), Now.AddDays(10));
        var unrelatedWindowId = await CreateWindowAsync(secretariat, unrelatedTypeId, "Oct 2026", Now.AddDays(-10), 3, Now.AddDays(20), Now.AddDays(10));

        var (headClient, headUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(headUserId, muqamId, RoleName.UnitHead);
        await CreateSubmittedInstanceAsync(windowId, muqamId, headUserId);

        var otherHeadId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Hist7");
        await factory.CreateRoleAssignmentAsync(otherHeadId, otherMuqamId, RoleName.UnitHead);
        await CreateSubmittedInstanceAsync(unrelatedWindowId, otherMuqamId, otherHeadId);

        var filters = await (await headClient.GetAsync(new Uri("/api/v1/history/filters", UriKind.Relative)))
            .Content.ReadFromJsonAsync<HistoryFilterOptions>();

        Assert.Contains(filters!.ReportTypes, r => r.Id == ownTypeId);
        Assert.DoesNotContain(filters.ReportTypes, r => r.Id == unrelatedTypeId);
        Assert.Single(filters.Periods);
    }

    [Fact]
    public async Task CallerWithNoAccessibleUnitsGetsAnEmptyPageNotAnError()
    {
        var (client, _) = await CreateAuthenticatedUserAsync();

        var page = await GetHistoryAsync(client);

        Assert.Empty(page.Rows);
        Assert.Equal(0, page.TotalCount);
    }
}
