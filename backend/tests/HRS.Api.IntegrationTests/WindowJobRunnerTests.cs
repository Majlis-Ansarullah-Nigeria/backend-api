using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Windows.Jobs;
using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Templates;
using HRS.Domain.Windows;
using HRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

/// <summary>
/// WindowJobRunner has no HTTP endpoint of its own (M4.4 is a background worker, not an API) —
/// these tests call it directly against the real test database, the same way the Worker's
/// recurring job does.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class WindowJobRunnerTests(ApiFactory factory)
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
        AllowAttachments: true);

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Job1";
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

    // "WJ"-prefixed codes, not the plain Z/D/M or W-only prefixes other files use — every test in
    // HRS.Api.IntegrationTests shares one Testcontainers Postgres, so unit codes must be
    // namespaced per file to avoid (ParentId, Code) collisions across files.
    private static async Task<Guid> CreateMuqamUnitAsync(HttpClient client, string suffix)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"WJZ{suffix}", $"Job Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"WJD{suffix}", $"Job Dila {suffix}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var muqam = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"WJM{suffix}", $"Job Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dila!.Id, null, null, null, null, null)))
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
            "Job Test Window", opensAt, graceDays, closesAt, [new WindowDueDate(UnitLevelCode.Muqam, dueAt)], targetUnitIds);
        var created = await (await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        return created!.Window.Id;
    }

    private async Task<int> RunJobAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        return await WindowJobRunner.RunAsync(db, clock, CancellationToken.None);
    }

    private async Task<List<WindowJobRun>> GetJobRunsAsync(Guid windowId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.WindowJobRuns.AsNoTracking().Where(r => r.WindowId == windowId).ToListAsync();
    }

    [Fact]
    public async Task OpenedIsRecordedOnceForAUnitInAnOpenWindow()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Opened Job Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(client, "01");
        var windowId = await CreateWindowAsync(client, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var created = await RunJobAsync();

        var runs = await GetJobRunsAsync(windowId);
        Assert.Contains(runs, r => r.UnitId == muqamId && r.EventType == WindowJobEventType.Opened);
        Assert.True(created > 0);
    }

    [Fact]
    public async Task RunningTwiceNeverDuplicatesAnEvent()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Idempotent Job Report", UnitLevelCode.Muqam);
        await CreateMuqamUnitAsync(client, "02");
        var windowId = await CreateWindowAsync(client, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        await RunJobAsync();
        var firstRunCount = (await GetJobRunsAsync(windowId)).Count;
        var createdOnSecondRun = await RunJobAsync();
        var secondRunCount = (await GetJobRunsAsync(windowId)).Count;

        Assert.Equal(firstRunCount, secondRunCount);
        Assert.Equal(0, createdOnSecondRun);
    }

    [Fact]
    public async Task ReminderEventsFireOnceDueDateIsWithinAConfiguredThreshold()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Reminder Job Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(client, "03");
        // Due in 18 hours — within the "1 day before" threshold.
        var windowId = await CreateWindowAsync(client, reportTypeId, Now.AddDays(-5), 3, Now.AddDays(30), Now.AddHours(18));

        await RunJobAsync();

        var runs = await GetJobRunsAsync(windowId);
        Assert.Contains(runs, r => r.UnitId == muqamId && r.EventType == WindowJobEventType.Reminder && r.EventKey == "1");
    }

    [Fact]
    public async Task OverdueIsRecordedWhilePastDueButWithinGrace()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Overdue Job Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(client, "04");
        var windowId = await CreateWindowAsync(client, reportTypeId, Now.AddDays(-10), 3, Now.AddDays(10), Now.AddHours(-1));

        await RunJobAsync();

        var runs = await GetJobRunsAsync(windowId);
        Assert.Contains(runs, r => r.UnitId == muqamId && r.EventType == WindowJobEventType.Overdue);
    }

    [Fact]
    public async Task ClosedIsRecordedOnceTheEffectiveCloseHasPassed()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Closed Job Report", UnitLevelCode.Muqam);
        var muqamId = await CreateMuqamUnitAsync(client, "05");
        var windowId = await CreateWindowAsync(client, reportTypeId, Now.AddDays(-30), 0, Now.AddHours(-1), Now.AddDays(-10));

        await RunJobAsync();

        var runs = await GetJobRunsAsync(windowId);
        Assert.Contains(runs, r => r.UnitId == muqamId && r.EventType == WindowJobEventType.Closed);
    }

    [Fact]
    public async Task OnlyTargetedUnitsGetEventsForAnAdHocWindow()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Scoped Job Report", UnitLevelCode.Muqam);
        var targetedId = await CreateMuqamUnitAsync(client, "06a");
        var otherId = await CreateMuqamUnitAsync(client, "06b");
        var windowId = await CreateWindowAsync(
            client, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10), [targetedId]);

        await RunJobAsync();

        var runs = await GetJobRunsAsync(windowId);
        Assert.Contains(runs, r => r.UnitId == targetedId);
        Assert.DoesNotContain(runs, r => r.UnitId == otherId);
    }

    [Fact]
    public async Task ArchivedUnitsAreSkippedWhenScopeIsAllUnitsAtALevel()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Archived Skip Job Report", UnitLevelCode.Muqam);
        var archivedId = await CreateMuqamUnitAsync(client, "07");
        await client.DeleteAsync(new Uri($"/api/v1/units/{archivedId}", UriKind.Relative));
        var windowId = await CreateWindowAsync(client, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        await RunJobAsync();

        var runs = await GetJobRunsAsync(windowId);
        Assert.DoesNotContain(runs, r => r.UnitId == archivedId);
    }
}
