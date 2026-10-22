using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Analytics;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Templates.FieldTargets;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Domain.Windows;
using HRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

/// <summary>FR-DSH-002/003/005 indicator dashboards, FR-TPL-011 targets and the Leadership overview.</summary>
[Collection(ApiTestCollection.Name)]
public class IndicatorsEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private async Task<(HttpClient Client, Guid UserId)> CreateClientAsync(RoleName? role, Guid unitId)
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Ind1";
        var userId = await factory.CreateUserAsync(email, password);
        if (role is { } r)
        {
            await factory.CreateRoleAssignmentAsync(userId, unitId, r);
        }

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        return (client, userId);
    }

    private static TemplateField Field(string key, TemplateFieldType type, string label, AggregationRule aggregation) =>
        new(key, type, label, null, false, null, null, null, null, null, false, null, null, null, aggregation, null, null, null);

    private sealed record Setup(
        HttpClient Secretariat, Guid ReportTypeId, Guid ZoneId, Guid DilaAId, Guid DilaBId, Guid MuqamA1Id, Guid PastWindowId, Guid CurrentWindowId);

    /// <summary>A report type filed by Dilaat and Muqamaat, with members (Sum), funds (Sum) and
    /// attendance (Average). One zone, Dila A (one Muqam) and Dila B. Both Dilaat file in two
    /// windows; every report is acknowledged through the real endpoint except Dila B's current
    /// one, which stays Submitted and must not count.</summary>
    private async Task<Setup> SetUpAsync(string suffix)
    {
        var (secretariat, _) = await CreateClientAsync(RoleName.Secretariat, OrganizationSeedIds.NationalUnitId);

        var reportType = await (await secretariat.PostAsJsonAsync("/api/v1/report-types", new CreateReportTypeRequest(
                $"Indicator Report {suffix}", null, ReportTypeCategory.Survey, ReportTypeFrequency.Monthly,
                [UnitLevelCode.Dila, UnitLevelCode.Muqam], UnitLevelCode.National, 1, AllowAttachments: false, AllowNilReport: false)))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        var schema = new TemplateSchema([new TemplateSection("figures", "Figures",
        [
            Field("members", TemplateFieldType.Number, "Total members", AggregationRule.Sum),
            Field("funds", TemplateFieldType.Currency, "Funds collected", AggregationRule.Sum),
            Field("attendance", TemplateFieldType.Percentage, "Attendance rate", AggregationRule.Average),
            Field("notes", TemplateFieldType.ShortText, "Notes", AggregationRule.None),
        ])]);
        var draft = await (await secretariat.PostAsJsonAsync(
                $"/api/v1/report-types/{reportType!.Id}/template-versions", new UpdateTemplateSchemaRequest(schema)))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await secretariat.PostAsync(new Uri($"/api/v1/report-types/{reportType.Id}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);

        // "IN"-prefixed codes — the shared Testcontainers Postgres needs per-file namespacing.
        async Task<Guid> CreateUnitAsync(string code, string name, Guid levelId, Guid parentId) =>
            (await (await secretariat.PostAsJsonAsync("/api/v1/units",
                new CreateUnitRequest(code, name, levelId, parentId, null, null, null, null, null)))
                .Content.ReadFromJsonAsync<UnitSummary>())!.Id;

        var zoneId = await CreateUnitAsync($"INZ{suffix}", $"Indicator Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId);
        var dilaAId = await CreateUnitAsync($"INDA{suffix}", $"Indicator Dila A {suffix}", OrganizationSeedIds.DilaLevelId, zoneId);
        var dilaBId = await CreateUnitAsync($"INDB{suffix}", $"Indicator Dila B {suffix}", OrganizationSeedIds.DilaLevelId, zoneId);
        var muqamA1Id = await CreateUnitAsync($"INM{suffix}", $"Indicator Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dilaAId);

        var toAcknowledge = new List<Guid>();
        Guid pastId, currentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var versionId = (await db.TemplateVersions.FirstAsync(t => t.ReportTypeId == reportType.Id && t.Status == TemplateVersionStatus.Published)).Id;
            var dueDates = new[] { new WindowLevelDueDate(UnitLevelCode.Dila, Now.AddDays(5)), new WindowLevelDueDate(UnitLevelCode.Muqam, Now.AddDays(5)) };
            var past = SubmissionWindow.Create(Guid.CreateVersion7(), reportType.Id, "Aug 2026", Now.AddDays(-40), 3, Now.AddDays(-25),
                dueDates.Select(d => new WindowLevelDueDate(d.Level, Now.AddDays(-32))), clock.UtcNow);
            var current = SubmissionWindow.Create(Guid.CreateVersion7(), reportType.Id, "Sep 2026", Now.AddDays(-5), 3, Now.AddDays(15), dueDates, clock.UtcNow);
            db.SubmissionWindows.AddRange(past, current);
            pastId = past.Id;
            currentId = current.Id;

            ReportInstance Submitted(SubmissionWindow window, Guid unitId, string answers)
            {
                var instance = ReportInstance.Create(Guid.CreateVersion7(), window.Id, unitId, versionId, clock.UtcNow);
                instance.Save(answers);
                instance.Submit(isLate: false, $"REF-IN-{Guid.NewGuid():N}", isNil: false, Now);
                db.ReportInstances.Add(instance);
                return instance;
            }

            toAcknowledge.Add(Submitted(past, dilaAId, """{"members":100,"funds":1000,"attendance":50,"notes":"x"}""").Id);
            toAcknowledge.Add(Submitted(past, dilaBId, """{"members":60,"funds":"500","attendance":70}""").Id);
            toAcknowledge.Add(Submitted(past, muqamA1Id, """{"members":40}""").Id);
            toAcknowledge.Add(Submitted(current, dilaAId, """{"members":110,"funds":1200,"attendance":60}""").Id);
            Submitted(current, dilaBId, """{"members":999,"funds":9999,"attendance":99}""");
            await db.SaveChangesAsync();
        }

        foreach (var id in toAcknowledge)
        {
            var response = await secretariat.PostAsync(new Uri($"/api/v1/secretariat/inbox/{id}/acknowledge", UriKind.Relative), null);
            Assert.True(response.IsSuccessStatusCode, $"Acknowledge failed: {(int)response.StatusCode}");
        }

        return new Setup(secretariat, reportType.Id, zoneId, dilaAId, dilaBId, muqamA1Id, pastId, currentId);
    }

    private static string Url(Setup setup, string? query = null) =>
        $"/api/v1/dashboards/indicators?reportTypeId={setup.ReportTypeId}{(query is null ? string.Empty : "&" + query)}";

    [Fact]
    public async Task AZoneCombinesItsDilaatsOwnAcknowledgedReportsAndDrillsDownToThem()
    {
        var setup = await SetUpAsync("01");
        using var secretariat = setup.Secretariat;

        var zone = await secretariat.GetFromJsonAsync<IndicatorDashboard>(Url(setup, $"unitId={setup.ZoneId}"));

        Assert.Equal(["members", "funds", "attendance"], zone!.Fields.Select(f => f.FieldKey));
        Assert.Equal("members", zone.FieldKey);
        Assert.Equal(["Aug 2026", "Sep 2026"], zone.Periods.Select(p => p.PeriodLabel));
        Assert.Equal([OrganizationSeedIds.NationalUnitId, setup.ZoneId], zone.Breadcrumb.Select(b => b.UnitId));

        var series = zone.Series.ToDictionary(s => s.FieldKey);
        Assert.Equal([160d, 110d], series["members"].Values.Select(v => v!.Value));
        Assert.Equal([1500d, 1200d], series["funds"].Values.Select(v => v!.Value));
        Assert.Equal([60d, 60d], series["attendance"].Values.Select(v => v!.Value));

        Assert.Equal(
            [("Indicator Dila A 01", true, 100d, 110d), ("Indicator Dila B 01", false, 60d, 60d)],
            zone.Children.Select(c => (c.Name, c.HasChildren, c.First!.Value, c.Latest!.Value)));
    }

    [Fact]
    public async Task TargetsAreSetPerLevelAndSumUpTheTree()
    {
        var setup = await SetUpAsync("02");
        using var secretariat = setup.Secretariat;

        var set = await secretariat.PutAsJsonAsync($"/api/v1/report-types/{setup.ReportTypeId}/field-targets/members",
            new SetFieldTargetsRequest([new FieldTargetLevelValue(UnitLevelCode.Dila, 120), new FieldTargetLevelValue(UnitLevelCode.Muqam, 50)]));
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);

        var zone = await secretariat.GetFromJsonAsync<IndicatorDashboard>(Url(setup, $"unitId={setup.ZoneId}"));
        Assert.Equal(240, zone!.Series.Single(s => s.FieldKey == "members").Target);
        Assert.All(zone.Children, c => Assert.Equal(120, c.Target));

        var cleared = await secretariat.PutAsJsonAsync($"/api/v1/report-types/{setup.ReportTypeId}/field-targets/members",
            new SetFieldTargetsRequest([new FieldTargetLevelValue(UnitLevelCode.Dila, null)]));
        var remaining = await cleared.Content.ReadFromJsonAsync<List<FieldTargetDto>>();
        Assert.Equal([(UnitLevelCode.Muqam, 50m)], remaining!.Select(t => (t.Level, t.Value)));
    }

    [Fact]
    public async Task TargetsRejectOtherLevelsNegativeValuesAndNonSecretariatUsers()
    {
        var setup = await SetUpAsync("03");
        using var secretariat = setup.Secretariat;
        var (head, _) = await CreateClientAsync(RoleName.UnitHead, setup.DilaAId);
        var url = $"/api/v1/report-types/{setup.ReportTypeId}/field-targets/members";

        var zoneLevel = await secretariat.PutAsJsonAsync(url, new SetFieldTargetsRequest([new FieldTargetLevelValue(UnitLevelCode.Zone, 5)]));
        var negative = await secretariat.PutAsJsonAsync(url, new SetFieldTargetsRequest([new FieldTargetLevelValue(UnitLevelCode.Dila, -1)]));
        var byHead = await head.PutAsJsonAsync(url, new SetFieldTargetsRequest([new FieldTargetLevelValue(UnitLevelCode.Dila, 5)]));

        Assert.Equal(HttpStatusCode.BadRequest, zoneLevel.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byHead.StatusCode);
    }

    [Fact]
    public async Task ADilaHeadSeesOnlyTheirOwnSubtree()
    {
        var setup = await SetUpAsync("04");
        setup.Secretariat.Dispose();
        var (dilaHead, _) = await CreateClientAsync(RoleName.UnitHead, setup.DilaAId);
        var (muqamHead, _) = await CreateClientAsync(RoleName.UnitHead, setup.MuqamA1Id);

        var own = await dilaHead.GetFromJsonAsync<IndicatorDashboard>(Url(setup));
        var child = await dilaHead.GetAsync(new Uri(Url(setup, $"unitId={setup.MuqamA1Id}"), UriKind.Relative));
        var zone = await dilaHead.GetAsync(new Uri(Url(setup, $"unitId={setup.ZoneId}"), UriKind.Relative));
        var sibling = await dilaHead.GetAsync(new Uri(Url(setup, $"unitId={setup.DilaBId}"), UriKind.Relative));
        var muqam = await muqamHead.GetAsync(new Uri(Url(setup), UriKind.Relative));
        var dilaAccess = await dilaHead.GetFromJsonAsync<DashboardAccess>("/api/v1/dashboards/access");
        var muqamAccess = await muqamHead.GetFromJsonAsync<DashboardAccess>("/api/v1/dashboards/access");

        Assert.Equal(setup.DilaAId, own!.Node!.UnitId);
        Assert.Equal([setup.DilaAId], own.Breadcrumb.Select(b => b.UnitId));
        Assert.Equal([100d, 110d], own.Series.Single(s => s.FieldKey == "members").Values.Select(v => v!.Value));
        Assert.Equal(HttpStatusCode.OK, child.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, zone.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, sibling.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, muqam.StatusCode);
        Assert.Equal((true, false), (dilaAccess!.HasIndicators, dilaAccess.HasCompliance));
        Assert.Equal((false, false), (muqamAccess!.HasIndicators, muqamAccess.HasCompliance));
    }

    [Fact]
    public async Task LeadershipGetsTheMobileOverviewAndHeadsDont()
    {
        var setup = await SetUpAsync("05");
        setup.Secretariat.Dispose();
        var (leadership, _) = await CreateClientAsync(RoleName.Leadership, OrganizationSeedIds.NationalUnitId);
        var (head, _) = await CreateClientAsync(RoleName.UnitHead, setup.DilaAId);

        var overview = await leadership.GetAsync(new Uri($"/api/v1/leadership/overview?period={(int)LeadershipPeriod.Year}", UriKind.Relative));
        var forbidden = await head.GetAsync(new Uri("/api/v1/leadership/overview", UriKind.Relative));
        var access = await leadership.GetFromJsonAsync<DashboardAccess>("/api/v1/dashboards/access");

        Assert.Equal(HttpStatusCode.OK, overview.StatusCode);
        var body = await overview.Content.ReadFromJsonAsync<LeadershipOverview>();
        Assert.Equal(LeadershipPeriod.Year, body!.Period);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal((true, true), (access!.HasIndicators, access.HasCompliance));
    }
}
