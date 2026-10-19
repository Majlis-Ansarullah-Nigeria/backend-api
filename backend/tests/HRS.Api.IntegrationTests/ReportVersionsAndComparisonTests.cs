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
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

/// <summary>FR-HIS-005 (period comparison) and FR-HIS-006 (version history with diffs).</summary>
[Collection(ApiTestCollection.Name)]
public class ReportVersionsAndComparisonTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Vers1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedUserAsync(string name = "Test User")
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Vers2";
        var userId = await factory.CreateUserAsync(email, password, name: name);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, userId);
    }

    private static async Task<Guid> CreateReportTypeAsync(HttpClient client, string name)
    {
        var request = new CreateReportTypeRequest(
            name, "Monthly activity figures", ReportTypeCategory.GeneralMonthly, ReportTypeFrequency.Monthly,
            [UnitLevelCode.Muqam], UnitLevelCode.National, ObserverDepth: 1, AllowAttachments: true, AllowNilReport: false);
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", request)).Content.ReadFromJsonAsync<ReportTypeSummary>();
        return created!.Id;
    }

    private static TemplateField Field(string key, TemplateFieldType type, string label, bool confidential = false) =>
        new(key, type, label, null, false, null, null, null, null, null, confidential,
            null, null, null, AggregationRule.None, null, null, null);

    /// <summary>Two figures, one text field in its own section, and one confidential figure.</summary>
    private static async Task PublishTemplateAsync(HttpClient client, Guid reportTypeId)
    {
        var schema = new TemplateSchema(
        [
            new TemplateSection("figures", "Figures",
            [
                Field("members", TemplateFieldType.Number, "Total members"),
                Field("funds", TemplateFieldType.Currency, "Funds collected"),
                Field("welfare_cases", TemplateFieldType.Number, "Welfare cases", confidential: true),
            ]),
            new TemplateSection("highlights", "Highlights", [Field("highlights", TemplateFieldType.LongText, "Highlights")]),
        ]);

        var draft = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(schema)))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);
    }

    // "RV"-prefixed codes — every test in HRS.Api.IntegrationTests shares one Testcontainers
    // Postgres, so unit codes must be namespaced per file to avoid (ParentId, Code) collisions.
    private static async Task<(Guid DilaId, Guid MuqamId)> CreateDilaWithOneMuqamAsync(HttpClient client, string suffix)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"RVZ{suffix}", $"Versions Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"RVD{suffix}", $"Versions Dila {suffix}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var muqam = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"RVM{suffix}", $"Versions Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dila!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();

        return (dila.Id, muqam!.Id);
    }

    private static async Task<Guid> CreateWindowAsync(HttpClient client, Guid reportTypeId, string periodLabel, int opensDaysAgo)
    {
        var request = new CreateSubmissionWindowRequest(
            periodLabel, Now.AddDays(-opensDaysAgo), 3, Now.AddDays(20), [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))]);
        var created = await (await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        return created!.Window.Id;
    }

    /// <summary>Writes a report instance directly in the given state — Submitted, or a saved Draft.</summary>
    private async Task<Guid> CreateInstanceAsync(Guid windowId, Guid unitId, string answersJson, bool submit = true)
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
        if (submit)
        {
            instance.Submit(isLate: false, $"REF-RV-{Guid.NewGuid():N}", isNil: false, Now, null);
        }

        db.ReportInstances.Add(instance);
        await db.SaveChangesAsync();

        return instance.Id;
    }

    /// <summary>Autosave with the report's current ETag as If-Match (FR-SUB-003) — submit and
    /// withdraw both bump the row version, so it's re-read before every save.</summary>
    private static async Task SaveAsync(HttpClient client, Guid windowId, Guid unitId, Dictionary<string, object?> answers)
    {
        var url = $"/api/v1/reports/{windowId}?unit={unitId}";
        var current = await client.GetAsync(new Uri(url, UriKind.Relative));
        using var message = new HttpRequestMessage(HttpMethod.Put, new Uri(url, UriKind.Relative))
        {
            Content = JsonContent.Create(new SaveReportDraftRequest(answers)),
        };
        if (current.Headers.ETag is { } etag)
        {
            message.Headers.TryAddWithoutValidation("If-Match", etag.Tag);
        }

        var response = await client.SendAsync(message);
        Assert.True(response.IsSuccessStatusCode, $"Save failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid windowId, Guid unitId) =>
        client.PostAsJsonAsync($"/api/v1/reports/{windowId}/submit?unit={unitId}", new SubmitReportRequest(Confirmed: true));

    private static Task<HttpResponseMessage> WithdrawAsync(HttpClient client, Guid windowId, Guid unitId) =>
        client.PostAsync(new Uri($"/api/v1/reports/{windowId}/withdraw?unit={unitId}", UriKind.Relative), null);

    private sealed record Setup(HttpClient Secretariat, Guid ReportTypeId, Guid DilaId, Guid MuqamId);

    private async Task<Setup> SetUpAsync(string suffix)
    {
        var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, $"Versions Report {suffix}");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamId) = await CreateDilaWithOneMuqamAsync(secretariat, suffix);
        return new Setup(secretariat, reportTypeId, dilaId, muqamId);
    }

    [Fact]
    public async Task SubmitWithdrawAndResubmitAreRecordedNewestFirstWithADiff()
    {
        var setup = await SetUpAsync("01");
        using var secretariat = setup.Secretariat;
        var windowId = await CreateWindowAsync(secretariat, setup.ReportTypeId, "Oct 2026", 1);
        var (head, headUserId) = await CreateAuthenticatedUserAsync("Amina Bello");
        await factory.CreateRoleAssignmentAsync(headUserId, setup.MuqamId, RoleName.UnitHead);

        await SaveAsync(head, windowId, setup.MuqamId, new() { ["members"] = 142, ["highlights"] = "Quiet month." });
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(head, windowId, setup.MuqamId)).StatusCode);
        Assert.True((await WithdrawAsync(head, windowId, setup.MuqamId)).IsSuccessStatusCode);
        await SaveAsync(head, windowId, setup.MuqamId, new() { ["members"] = 148, ["highlights"] = "Quiet month." });
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(head, windowId, setup.MuqamId)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instanceId = (await db.ReportInstances.AsNoTracking().FirstAsync(i => i.WindowId == windowId && i.UnitId == setup.MuqamId)).Id;

        var history = await head.GetFromJsonAsync<ReportRevisionHistory>($"/api/v1/reports/{instanceId}/revisions");

        Assert.Equal(
            [(3, ReportRevisionAction.Resubmitted), (2, ReportRevisionAction.Withdrawn), (1, ReportRevisionAction.Submitted)],
            history!.Revisions.Select(r => (r.RevisionNo, r.Action)));
        Assert.All(history.Revisions, r => Assert.Equal("Amina Bello", r.ActorName));
        var change = Assert.Single(history.Revisions[0].Changes);
        Assert.Equal(("members", "142", "148"), (change.FieldKey, change.Before, change.After));
        Assert.Empty(history.Revisions[1].Changes);
        Assert.Empty(history.Revisions[2].Changes);
    }

    [Fact]
    public async Task ObserversGetRevisionsWithoutConfidentialChangesAndTheSecretariatGetsThemAll()
    {
        var setup = await SetUpAsync("02");
        using var secretariat = setup.Secretariat;
        var windowId = await CreateWindowAsync(secretariat, setup.ReportTypeId, "Oct 2026", 1);
        var (head, headUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(headUserId, setup.MuqamId, RoleName.UnitHead);
        var (dila, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, setup.DilaId, RoleName.UnitHead);

        await SaveAsync(head, windowId, setup.MuqamId, new() { ["members"] = 10, ["welfare_cases"] = 1 });
        await SubmitAsync(head, windowId, setup.MuqamId);
        await WithdrawAsync(head, windowId, setup.MuqamId);
        await SaveAsync(head, windowId, setup.MuqamId, new() { ["members"] = 11, ["welfare_cases"] = 2 });
        await SubmitAsync(head, windowId, setup.MuqamId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instanceId = (await db.ReportInstances.AsNoTracking().FirstAsync(i => i.WindowId == windowId && i.UnitId == setup.MuqamId)).Id;

        var observed = await dila.GetFromJsonAsync<ReportRevisionHistory>($"/api/v1/reports/{instanceId}/revisions");
        Assert.Equal(["members"], observed!.Revisions[0].Changes.Select(c => c.FieldKey));

        var full = await secretariat.GetFromJsonAsync<ReportRevisionHistory>($"/api/v1/reports/{instanceId}/revisions");
        Assert.Equal(["members", "welfare_cases"], full!.Revisions[0].Changes.Select(c => c.FieldKey));
    }

    [Fact]
    public async Task AnUnrelatedUserCannotReadRevisionsOrCompare()
    {
        var setup = await SetUpAsync("03");
        using var secretariat = setup.Secretariat;
        var windowId = await CreateWindowAsync(secretariat, setup.ReportTypeId, "Oct 2026", 1);
        var instanceId = await CreateInstanceAsync(windowId, setup.MuqamId, """{"members":5}""");
        var (stranger, _) = await CreateAuthenticatedUserAsync();

        var revisions = await stranger.GetAsync(new Uri($"/api/v1/reports/{instanceId}/revisions", UriKind.Relative));
        var compare = await stranger.GetAsync(new Uri(
            $"/api/v1/history/compare?unitId={setup.MuqamId}&reportTypeId={setup.ReportTypeId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, revisions.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, compare.StatusCode);
    }

    [Fact]
    public async Task CompareDefaultsToTheTwoLatestPeriodsWithFiguresTextChangesAndATrend()
    {
        var setup = await SetUpAsync("04");
        using var secretariat = setup.Secretariat;
        var julyId = await CreateWindowAsync(secretariat, setup.ReportTypeId, "Jul 2026", 90);
        var augustId = await CreateWindowAsync(secretariat, setup.ReportTypeId, "Aug 2026", 60);
        var septemberId = await CreateWindowAsync(secretariat, setup.ReportTypeId, "Sep 2026", 30);
        await CreateInstanceAsync(julyId, setup.MuqamId, """{"members":140,"funds":390000,"highlights":"A"}""");
        await CreateInstanceAsync(augustId, setup.MuqamId, """{"members":142,"funds":398000,"welfare_cases":3,"highlights":"A"}""");
        await CreateInstanceAsync(septemberId, setup.MuqamId, """{"members":148,"funds":412500,"welfare_cases":1,"highlights":"B"}""");
        var (head, headUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(headUserId, setup.MuqamId, RoleName.UnitHead);

        var comparison = await head.GetFromJsonAsync<PeriodComparison>(
            $"/api/v1/history/compare?unitId={setup.MuqamId}&reportTypeId={setup.ReportTypeId}");

        Assert.Equal((augustId, septemberId), (comparison!.PeriodAWindowId!.Value, comparison.PeriodBWindowId!.Value));
        Assert.Equal(["Jul 2026", "Aug 2026", "Sep 2026"], comparison.Periods.Select(p => p.PeriodLabel));
        Assert.Equal(
            [("members", 142d, 148d), ("funds", 398000d, 412500d), ("welfare_cases", 3d, 1d)],
            comparison.Figures.Select(f => (f.FieldKey, f.PeriodA!.Value, f.PeriodB!.Value)));
        Assert.Equal(["Highlights"], comparison.ChangedTextSections);
        Assert.Equal([140d, 142d, 148d], comparison.Series.Select(p => p.Values["members"]!.Value));

        var explicitPeriods = await head.GetFromJsonAsync<PeriodComparison>(
            $"/api/v1/history/compare?unitId={setup.MuqamId}&reportTypeId={setup.ReportTypeId}&periodA={julyId}&periodB={augustId}");
        Assert.Equal((julyId, augustId), (explicitPeriods!.PeriodAWindowId!.Value, explicitPeriods.PeriodBWindowId!.Value));
        Assert.Equal(2, explicitPeriods.Series.Count);
    }

    [Fact]
    public async Task OnlyTheUnitsOwnHeadSeesTheDraftAndObserversDontSeeConfidentialFigures()
    {
        var setup = await SetUpAsync("05");
        using var secretariat = setup.Secretariat;
        var augustId = await CreateWindowAsync(secretariat, setup.ReportTypeId, "Aug 2026", 60);
        var septemberId = await CreateWindowAsync(secretariat, setup.ReportTypeId, "Sep 2026", 30);
        await CreateInstanceAsync(augustId, setup.MuqamId, """{"members":142,"welfare_cases":3}""");
        await CreateInstanceAsync(septemberId, setup.MuqamId, """{"members":148,"welfare_cases":1}""", submit: false);
        var (head, headUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(headUserId, setup.MuqamId, RoleName.UnitHead);
        var (dila, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, setup.DilaId, RoleName.UnitHead);
        var url = $"/api/v1/history/compare?unitId={setup.MuqamId}&reportTypeId={setup.ReportTypeId}";

        var own = await head.GetFromJsonAsync<PeriodComparison>(url);
        var observed = await dila.GetFromJsonAsync<PeriodComparison>(url);
        var recipient = await secretariat.GetFromJsonAsync<PeriodComparison>(url);

        Assert.Equal([false, true], own!.Periods.Select(p => p.IsDraft));
        Assert.Contains(own.Figures, f => f.FieldKey == "welfare_cases");

        Assert.Equal([augustId], observed!.Periods.Select(p => p.WindowId));
        Assert.DoesNotContain(observed.Figures, f => f.FieldKey == "welfare_cases");

        Assert.Equal([augustId], recipient!.Periods.Select(p => p.WindowId));
        Assert.Contains(recipient.Figures, f => f.FieldKey == "welfare_cases");
    }

    [Fact]
    public async Task CompareOptionsListOwnAndObservedUnitsWithTheirReportTypes()
    {
        var setup = await SetUpAsync("06");
        using var secretariat = setup.Secretariat;
        var windowId = await CreateWindowAsync(secretariat, setup.ReportTypeId, "Oct 2026", 1);
        await CreateInstanceAsync(windowId, setup.MuqamId, """{"members":5}""");
        var (dila, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, setup.DilaId, RoleName.UnitHead);

        var options = await dila.GetFromJsonAsync<List<ComparisonUnitOption>>("/api/v1/history/compare/options");

        var muqam = Assert.Single(options!, o => o.UnitId == setup.MuqamId);
        Assert.Equal([setup.ReportTypeId], muqam.ReportTypes.Select(r => r.Id));
    }
}
