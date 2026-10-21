using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Analytics;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Access;
using HRS.Domain.Notifications;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Domain.Windows;
using HRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

/// <summary>FR-DSH-001: the compliance dashboard, its one-click reminders, and the Secretariat overview.</summary>
[Collection(ApiTestCollection.Name)]
public class DashboardsEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private async Task<(HttpClient Client, Guid UserId)> CreateClientAsync(RoleName? role, Guid unitId, string name = "Test User")
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Dash1";
        var userId = await factory.CreateUserAsync(email, password, name: name);
        if (role is { } r)
        {
            await factory.CreateRoleAssignmentAsync(userId, unitId, r);
        }

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        return (client, userId);
    }

    private sealed record Setup(HttpClient Secretariat, Guid ReportTypeId, Guid ZoneId, Guid[] MuqamIds, Guid PastWindowId, Guid CurrentWindowId, Guid[] HeadIds);

    /// <summary>One zone with four Muqamaat and two windows targeted at exactly those four, so the
    /// counts don't depend on units other tests create in the shared database. Past window: M1 on
    /// time, M2 late, M3 a draft (missed), M4 nothing (missed). Current window, open and not yet
    /// due: M1 submitted, M2 a draft, M3 and M4 nothing. M2 and M3 have heads; M4 is vacant.</summary>
    private async Task<Setup> SetUpAsync(string suffix, DateTimeOffset? currentDueAt = null)
    {
        var (secretariat, _) = await CreateClientAsync(RoleName.Secretariat, OrganizationSeedIds.NationalUnitId);

        var reportType = await (await secretariat.PostAsJsonAsync("/api/v1/report-types", new CreateReportTypeRequest(
                $"Dashboard Report {suffix}", null, ReportTypeCategory.GeneralMonthly, ReportTypeFrequency.Monthly,
                [UnitLevelCode.Muqam], UnitLevelCode.National, 1, AllowAttachments: false, AllowNilReport: false)))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        var notes = new TemplateField("notes", TemplateFieldType.ShortText, "Notes", null, false, null, null, null, null, null, false,
            null, null, null, AggregationRule.None, null, null, null);
        var draft = await (await secretariat.PostAsJsonAsync(
                $"/api/v1/report-types/{reportType!.Id}/template-versions",
                new UpdateTemplateSchemaRequest(new TemplateSchema([new TemplateSection("s1", "Section", [notes])]))))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await secretariat.PostAsync(new Uri($"/api/v1/report-types/{reportType.Id}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);

        // "DB"-prefixed codes — the shared Testcontainers Postgres needs per-file namespacing.
        async Task<Guid> CreateUnitAsync(string code, string name, Guid levelId, Guid parentId) =>
            (await (await secretariat.PostAsJsonAsync("/api/v1/units",
                new CreateUnitRequest(code, name, levelId, parentId, null, null, null, null, null)))
                .Content.ReadFromJsonAsync<UnitSummary>())!.Id;

        var zoneId = await CreateUnitAsync($"DBZ{suffix}", $"Dashboard Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId);
        var dilaId = await CreateUnitAsync($"DBD{suffix}", $"Dashboard Dila {suffix}", OrganizationSeedIds.DilaLevelId, zoneId);
        var muqamIds = new Guid[4];
        for (var i = 0; i < 4; i++)
        {
            muqamIds[i] = await CreateUnitAsync($"DBM{suffix}{i}", $"Dashboard Muqam {suffix}-{i}", OrganizationSeedIds.MuqamLevelId, dilaId);
        }

        var headIds = new Guid[2];
        for (var i = 0; i < 2; i++)
        {
            headIds[i] = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Dash2");
            await factory.CreateRoleAssignmentAsync(headIds[i], muqamIds[i + 1], RoleName.UnitHead);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var versionId = (await db.TemplateVersions.FirstAsync(t => t.ReportTypeId == reportType.Id && t.Status == TemplateVersionStatus.Published)).Id;

        var past = SubmissionWindow.Create(Guid.CreateVersion7(), reportType.Id, "Aug 2026", Now.AddDays(-40), 3, Now.AddDays(-25),
            [new WindowLevelDueDate(UnitLevelCode.Muqam, Now.AddDays(-32))], clock.UtcNow, targetUnitIds: muqamIds);
        var current = SubmissionWindow.Create(Guid.CreateVersion7(), reportType.Id, "Sep 2026", Now.AddDays(-5), 3, Now.AddDays(15),
            [new WindowLevelDueDate(UnitLevelCode.Muqam, currentDueAt ?? Now.AddDays(5))], clock.UtcNow, targetUnitIds: muqamIds);
        db.SubmissionWindows.AddRange(past, current);

        ReportInstance Instance(SubmissionWindow window, Guid unitId, bool? submittedLate)
        {
            var instance = ReportInstance.Create(Guid.CreateVersion7(), window.Id, unitId, versionId, clock.UtcNow);
            instance.Save("""{"notes":"x"}""");
            if (submittedLate is { } isLate)
            {
                instance.Submit(isLate, $"REF-DB-{Guid.NewGuid():N}", isNil: false, Now);
            }

            return instance;
        }

        db.ReportInstances.AddRange(
            Instance(past, muqamIds[0], submittedLate: false),
            Instance(past, muqamIds[1], submittedLate: true),
            Instance(past, muqamIds[2], submittedLate: null),
            Instance(current, muqamIds[0], submittedLate: false),
            Instance(current, muqamIds[1], submittedLate: null));
        await db.SaveChangesAsync();

        return new Setup(secretariat, reportType.Id, zoneId, muqamIds, past.Id, current.Id, headIds);
    }

    [Fact]
    public async Task TheDashboardSplitsEachPeriodAndGroupsTheLatestByZone()
    {
        var setup = await SetUpAsync("01");
        using var secretariat = setup.Secretariat;

        var dashboard = await secretariat.GetFromJsonAsync<ComplianceDashboard>(
            $"/api/v1/dashboards/compliance?reportTypeId={setup.ReportTypeId}&level={(int)UnitLevelCode.Muqam}");

        Assert.Equal(setup.ReportTypeId, dashboard!.ReportTypeId);
        Assert.Equal([UnitLevelCode.Muqam], dashboard.Levels.Select(l => l.Code));
        Assert.Equal(
            [
                ("Aug 2026", true, 4, 1, 1, 2, 0, 0),
                ("Sep 2026", false, 4, 1, 0, 0, 1, 2),
            ],
            dashboard.Periods.Select(p => (p.PeriodLabel, p.IsClosed, p.Expected, p.OnTime, p.Late, p.Missed, p.Drafting, p.NotStarted)));

        var row = Assert.Single(dashboard.Groups);
        Assert.Equal((setup.ZoneId, "Dashboard Zone 01", 4, 1, 1, 2, 3), (row.GroupUnitId!.Value, row.Name, row.Expected, row.Submitted, row.Drafting, row.NotStarted, row.Remindable));
        Assert.True(dashboard.CanRemind);
    }

    [Fact]
    public async Task RemindNotifiesTheHeadsOfUnitsThatHaventSubmittedWithReminderWordingBeforeTheDueDate()
    {
        var setup = await SetUpAsync("02");
        using var secretariat = setup.Secretariat;

        var response = await secretariat.PostAsJsonAsync("/api/v1/dashboards/compliance/reminders",
            new SendComplianceRemindersRequest(setup.CurrentWindowId, UnitLevelCode.Muqam, setup.ZoneId));
        var result = await response.Content.ReadFromJsonAsync<SendRemindersResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((2, 1), (result!.UnitsReminded, result.UnitsWithoutHead));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var messages = await db.OutboxMessages.AsNoTracking().Where(m => setup.HeadIds.Contains(m.RecipientUserId)).ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.All(messages, m => Assert.Equal(NotificationEventType.Reminder, m.EventType));

        // No rate limit: a second click sends again.
        await secretariat.PostAsJsonAsync("/api/v1/dashboards/compliance/reminders",
            new SendComplianceRemindersRequest(setup.CurrentWindowId, UnitLevelCode.Muqam, setup.ZoneId));
        Assert.Equal(4, await db.OutboxMessages.AsNoTracking().CountAsync(m => setup.HeadIds.Contains(m.RecipientUserId)));
    }

    [Fact]
    public async Task RemindAfterTheDueDateUsesOverdueWording()
    {
        var setup = await SetUpAsync("03", currentDueAt: Now.AddDays(-1));
        using var secretariat = setup.Secretariat;

        await secretariat.PostAsJsonAsync("/api/v1/dashboards/compliance/reminders",
            new SendComplianceRemindersRequest(setup.CurrentWindowId, UnitLevelCode.Muqam, setup.ZoneId));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var messages = await db.OutboxMessages.AsNoTracking().Where(m => setup.HeadIds.Contains(m.RecipientUserId)).ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.All(messages, m => Assert.Equal(NotificationEventType.Overdue, m.EventType));
    }

    [Fact]
    public async Task RemindingAClosedWindowIsRefused()
    {
        var setup = await SetUpAsync("04");
        using var secretariat = setup.Secretariat;

        var response = await secretariat.PostAsJsonAsync("/api/v1/dashboards/compliance/reminders",
            new SendComplianceRemindersRequest(setup.PastWindowId, UnitLevelCode.Muqam, setup.ZoneId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task LeadershipSeesTheDashboardReadOnlyAndHeadsSeeNeither()
    {
        var setup = await SetUpAsync("05");
        setup.Secretariat.Dispose();
        var (leadership, _) = await CreateClientAsync(RoleName.Leadership, OrganizationSeedIds.NationalUnitId);
        var (head, _) = await CreateClientAsync(RoleName.UnitHead, setup.MuqamIds[0]);
        var url = $"/api/v1/dashboards/compliance?reportTypeId={setup.ReportTypeId}";

        var leadershipView = await leadership.GetFromJsonAsync<ComplianceDashboard>(url);
        var leadershipRemind = await leadership.PostAsJsonAsync("/api/v1/dashboards/compliance/reminders",
            new SendComplianceRemindersRequest(setup.CurrentWindowId, UnitLevelCode.Muqam, setup.ZoneId));
        var leadershipOverview = await leadership.GetAsync(new Uri("/api/v1/secretariat/overview", UriKind.Relative));
        var headView = await head.GetAsync(new Uri(url, UriKind.Relative));
        var headOverview = await head.GetAsync(new Uri("/api/v1/secretariat/overview", UriKind.Relative));

        Assert.False(leadershipView!.CanRemind);
        Assert.Equal(2, leadershipView.Periods.Count);
        Assert.Equal(HttpStatusCode.Forbidden, leadershipRemind.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, leadershipOverview.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, headView.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, headOverview.StatusCode);
    }

    [Fact]
    public async Task TheOverviewGreetsTheSecretaryAndCountsTheInboxAndAttentionItems()
    {
        var (secretariat, secretaryId) = await CreateClientAsync(RoleName.Secretariat, OrganizationSeedIds.NationalUnitId, name: "Mariam Okafor");
        var setup = await SetUpAsync("06");
        setup.Secretariat.Dispose();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var submitted = await db.ReportInstances.FirstAsync(i => i.WindowId == setup.CurrentWindowId && i.UnitId == setup.MuqamIds[0]);
            submitted.AssignTo(secretaryId);
            submitted.SetFlagged(true);
            await db.SaveChangesAsync();
        }

        var overview = await secretariat.GetFromJsonAsync<SecretariatOverview>("/api/v1/secretariat/overview");

        Assert.Equal("Mariam Okafor", overview!.UserName);
        Assert.Equal(1, overview.AssignedToMe);
        Assert.True(overview.UnreadInInbox >= 1);
        Assert.True(overview.FlaggedReports >= 1);
        Assert.True(overview.VacantHeads >= 1);
        Assert.NotNull(overview.CurrentWindow);
        Assert.Equal(overview.ByLevel.Sum(l => l.Expected), overview.Expected);
    }
}
