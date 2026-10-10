using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Observer.ComplianceGrid;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Submissions.MyReports;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class ComplianceGridEndpointsTests(ApiFactory factory)
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
        const string password = "Correct-Horse-Grid1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedUserAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Grid2";
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

    // "CG"-prefixed codes — every test in HRS.Api.IntegrationTests shares one Testcontainers
    // Postgres, so unit codes must be namespaced per file to avoid (ParentId, Code) collisions.
    private static async Task<Guid> CreateZoneAsync(HttpClient client, string suffix)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"CGZ{suffix}", $"Compliance Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        return zone!.Id;
    }

    private static async Task<Guid> CreateDilaAsync(HttpClient client, Guid zoneId, string suffix)
    {
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"CGD{suffix}", $"Compliance Dila {suffix}", OrganizationSeedIds.DilaLevelId, zoneId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        return dila!.Id;
    }

    private static async Task<Guid> CreateMuqamAsync(HttpClient client, Guid dilaId, string suffix)
    {
        var muqam = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"CGM{suffix}", $"Compliance Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dilaId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        return muqam!.Id;
    }

    /// <summary>One Dila with the given number of Muqamaat children, all under a fresh Zone.</summary>
    private static async Task<(Guid DilaId, List<Guid> MuqamIds)> CreateDilaWithMuqamaatAsync(HttpClient client, string suffix, int childCount)
    {
        var zoneId = await CreateZoneAsync(client, suffix);
        var dilaId = await CreateDilaAsync(client, zoneId, suffix);

        var muqamIds = new List<Guid>();
        for (var i = 0; i < childCount; i++)
        {
            muqamIds.Add(await CreateMuqamAsync(client, dilaId, $"{suffix}{i}"));
        }

        return (dilaId, muqamIds);
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

    private async Task SaveDraftAsync(Guid windowId, Guid unitId)
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
        db.ReportInstances.Add(instance);
        await db.SaveChangesAsync();
    }

    private async Task SaveAndSubmitAsync(Guid windowId, Guid unitId, bool isLate)
    {
        await SaveDraftAsync(windowId, unitId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instance = await db.ReportInstances.FirstAsync(i => i.WindowId == windowId && i.UnitId == unitId);
        instance.Submit(isLate, $"REF-GRID-{Guid.NewGuid():N}", false, Now);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task DilaHeadSeesComplianceGridAcrossTheFullStatusSpread()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Compliance Status Report", observerDepth: 1, UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);

        var (dilaId, muqamIds) = await CreateDilaWithMuqamaatAsync(secretariat, "01", childCount: 3);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026", Now.AddDays(-10), 3, Now.AddDays(20), Now.AddDays(10));

        // Muqam 0: submitted on time. Muqam 1: draft. Muqam 2: untouched (not started).
        await SaveAndSubmitAsync(windowId, muqamIds[0], isLate: false);
        await SaveDraftAsync(windowId, muqamIds[1]);

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var response = await dilaClient.GetAsync(new Uri($"/api/v1/units/compliance-grid?reportTypeId={reportTypeId}", UriKind.Relative));
        var grid = await response.Content.ReadFromJsonAsync<ComplianceGridResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(dilaId, grid!.ParentUnitId);
        Assert.Equal("Muqam", grid.ChildLevelName);
        Assert.Equal("Muqamaat", grid.ChildLevelPluralName);
        Assert.Single(grid.Periods);
        Assert.Equal(3, grid.Rows.Count);

        var byUnit = grid.Rows.ToDictionary(r => r.UnitId);
        Assert.Equal(ReportLineStatus.Submitted, byUnit[muqamIds[0]].Cells[0].Status);
        Assert.Equal(ReportLineStatus.Draft, byUnit[muqamIds[1]].Cells[0].Status);
        Assert.Equal(ReportLineStatus.NotStarted, byUnit[muqamIds[2]].Cells[0].Status);
    }

    [Fact]
    public async Task LateSubmissionShowsSubmittedLateStatus()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Compliance Late Report", observerDepth: 1, UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);

        var (dilaId, muqamIds) = await CreateDilaWithMuqamaatAsync(secretariat, "02", childCount: 1);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026", Now.AddDays(-10), 3, Now.AddDays(20), Now.AddDays(10));
        await SaveAndSubmitAsync(windowId, muqamIds[0], isLate: true);

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var grid = await (await dilaClient.GetAsync(new Uri($"/api/v1/units/compliance-grid?reportTypeId={reportTypeId}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<ComplianceGridResult>();

        Assert.Equal(ReportLineStatus.SubmittedLate, grid!.Rows[0].Cells[0].Status);
    }

    [Fact]
    public async Task MuqamWithNoHeadAssignmentShowsAsVacant()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Compliance Vacant Report", observerDepth: 1, UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);

        var (dilaId, muqamIds) = await CreateDilaWithMuqamaatAsync(secretariat, "03", childCount: 1);
        await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026", Now.AddDays(-10), 3, Now.AddDays(20), Now.AddDays(10));

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var grid = await (await dilaClient.GetAsync(new Uri("/api/v1/units/compliance-grid", UriKind.Relative)))
            .Content.ReadFromJsonAsync<ComplianceGridResult>();

        var row = grid!.Rows.Single(r => r.UnitId == muqamIds[0]);
        Assert.True(row.HasVacantHead);
        Assert.Null(row.HeadName);
    }

    [Fact]
    public async Task ReportTypeWithObserverDepthZeroIsExcludedFromAvailableReportTypes()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var observableTypeId = await CreateReportTypeAsync(secretariat, "Compliance Observable Report", observerDepth: 1, UnitLevelCode.Muqam);
        var hiddenTypeId = await CreateReportTypeAsync(secretariat, "Compliance Hidden Report", observerDepth: 0, UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, observableTypeId);
        await PublishEmptyTemplateAsync(secretariat, hiddenTypeId);

        var (dilaId, _) = await CreateDilaWithMuqamaatAsync(secretariat, "04", childCount: 1);

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var grid = await (await dilaClient.GetAsync(new Uri("/api/v1/units/compliance-grid", UriKind.Relative)))
            .Content.ReadFromJsonAsync<ComplianceGridResult>();

        Assert.Contains(grid!.AvailableReportTypes, r => r.Id == observableTypeId);
        Assert.DoesNotContain(grid.AvailableReportTypes, r => r.Id == hiddenTypeId);
    }

    [Fact]
    public async Task CallerWithNoObservableUnitGetsAnEmptyResultNotAnError()
    {
        var (client, _) = await CreateAuthenticatedUserAsync();

        var response = await client.GetAsync(new Uri("/api/v1/units/compliance-grid", UriKind.Relative));
        var grid = await response.Content.ReadFromJsonAsync<ComplianceGridResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(grid!.ParentUnitId);
        Assert.Empty(grid.AvailableParentUnits);
        Assert.Empty(grid.Rows);
    }

    [Fact]
    public async Task MuqamHeadWithNoChildrenIsNotAnEligibleParent()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var (_, muqamIds) = await CreateDilaWithMuqamaatAsync(secretariat, "05", childCount: 1);

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamIds[0], RoleName.UnitHead);

        var grid = await (await client.GetAsync(new Uri("/api/v1/units/compliance-grid", UriKind.Relative)))
            .Content.ReadFromJsonAsync<ComplianceGridResult>();

        Assert.Null(grid!.ParentUnitId);
        Assert.Empty(grid.AvailableParentUnits);
    }

    [Fact]
    public async Task RequestingAnExplicitParentUnitIdTheCallerDoesNotHeadIsForbidden()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var (otherDilaId, _) = await CreateDilaWithMuqamaatAsync(secretariat, "06", childCount: 1);

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, otherDilaId, RoleName.Delegate, DelegateScope.DraftOnly);

        var response = await client.GetAsync(new Uri($"/api/v1/units/compliance-grid?parentUnitId={otherDilaId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CallerHeadingTwoParentUnitsSeesBothAsOptionsAndDefaultsToTheFirstByName()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var (dilaA, _) = await CreateDilaWithMuqamaatAsync(secretariat, "07a", childCount: 1);
        var (dilaB, _) = await CreateDilaWithMuqamaatAsync(secretariat, "07b", childCount: 1);

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, dilaA, RoleName.UnitHead);
        await factory.CreateRoleAssignmentAsync(userId, dilaB, RoleName.UnitHead);

        var grid = await (await client.GetAsync(new Uri("/api/v1/units/compliance-grid", UriKind.Relative)))
            .Content.ReadFromJsonAsync<ComplianceGridResult>();

        Assert.Equal(2, grid!.AvailableParentUnits.Count);
        Assert.Contains(grid.AvailableParentUnits, u => u.Id == dilaA);
        Assert.Contains(grid.AvailableParentUnits, u => u.Id == dilaB);
        Assert.NotNull(grid.ParentUnitId);
    }

    [Fact]
    public async Task PeriodsParameterLimitsToTheMostRecentWindowsOrderedOldestToNewest()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Compliance Periods Report", observerDepth: 1, UnitLevelCode.Muqam);
        await PublishEmptyTemplateAsync(secretariat, reportTypeId);
        var (dilaId, _) = await CreateDilaWithMuqamaatAsync(secretariat, "08", childCount: 1);

        await CreateWindowAsync(secretariat, reportTypeId, "Aug 2026", Now.AddDays(-80), 3, Now.AddDays(-50), Now.AddDays(-60));
        await CreateWindowAsync(secretariat, reportTypeId, "Sep 2026", Now.AddDays(-50), 3, Now.AddDays(-20), Now.AddDays(-30));
        await CreateWindowAsync(secretariat, reportTypeId, "Oct 2026", Now.AddDays(-10), 3, Now.AddDays(20), Now.AddDays(10));

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var grid = await (await dilaClient.GetAsync(new Uri($"/api/v1/units/compliance-grid?reportTypeId={reportTypeId}&periods=2", UriKind.Relative)))
            .Content.ReadFromJsonAsync<ComplianceGridResult>();

        Assert.Equal(2, grid!.Periods.Count);
        Assert.Equal("Sep 2026", grid.Periods[0].PeriodLabel);
        Assert.Equal("Oct 2026", grid.Periods[1].PeriodLabel);
    }
}
