using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Submissions.PullFigures;
using HRS.Application.Features.Submissions.ReportInstances;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class PullFiguresEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CreateReportTypeRequest MultiLevelReport(string name) => new(
        name,
        "Monthly activity figures",
        ReportTypeCategory.GeneralMonthly,
        ReportTypeFrequency.Monthly,
        [UnitLevelCode.Dila, UnitLevelCode.Muqam],
        UnitLevelCode.National,
        1,
        AllowAttachments: true, AllowNilReport: false);

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Pull1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedUserAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Pull2";
        var userId = await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, userId);
    }

    private static async Task<Guid> CreateReportTypeAsync(HttpClient client, string name)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MultiLevelReport(name)))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        return created!.Id;
    }

    /// <summary>Sum/Average/Max/Latest numeric fields plus a non-aggregated text field, so the
    /// test also proves "notes" is never pulled.</summary>
    private static async Task PublishTemplateAsync(HttpClient client, Guid reportTypeId)
    {
        TemplateField Numeric(string key, string label, AggregationRule aggregation) => new(
            key, TemplateFieldType.Number, label, null, false, null, null, null, null, null, false,
            null, null, null, aggregation, null, null, null);

        var notes = new TemplateField(
            "notes", TemplateFieldType.ShortText, "Notes", null, false, null, null, null, null, null, false,
            null, null, null, AggregationRule.None, null, null, null);

        var schema = new TemplateSchema([
            new TemplateSection("section_1", "Section 1", [
                Numeric("total_members", "Total members", AggregationRule.Sum),
                Numeric("avg_attendance", "Average attendance", AggregationRule.Average),
                Numeric("max_score", "Max score", AggregationRule.Max),
                Numeric("latest_value", "Latest value", AggregationRule.Latest),
                notes,
            ]),
        ]);

        var draft = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(schema)))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);
    }

    // "PF"-prefixed codes — every test in HRS.Api.IntegrationTests shares one Testcontainers
    // Postgres, so unit codes must be namespaced per file to avoid (ParentId, Code) collisions.
    private static async Task<(Guid DilaId, List<Guid> MuqamIds)> CreateDilaWithMuqamaatAsync(HttpClient client, string suffix, int childCount)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"PFZ{suffix}", $"Pull Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"PFD{suffix}", $"Pull Dila {suffix}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();

        var muqamIds = new List<Guid>();
        for (var i = 0; i < childCount; i++)
        {
            var muqam = await (await client.PostAsJsonAsync(
                    "/api/v1/units",
                    new CreateUnitRequest($"PFM{suffix}{i}", $"Pull Muqam {suffix}{i}", OrganizationSeedIds.MuqamLevelId, dila!.Id, null, null, null, null, null)))
                .Content.ReadFromJsonAsync<UnitSummary>();
            muqamIds.Add(muqam!.Id);
        }

        return (dila!.Id, muqamIds);
    }

    private static async Task<Guid> CreateWindowAsync(HttpClient client, Guid reportTypeId)
    {
        var request = new CreateSubmissionWindowRequest(
            "Sep 2026", Now.AddDays(-10), 3, Now.AddDays(20),
            [new WindowDueDate(UnitLevelCode.Dila, Now.AddDays(12)), new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))]);
        var created = await (await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        return created!.Window.Id;
    }

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, Guid reportId, Guid unitId, Dictionary<string, object?> answers, string? ifMatch = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Put, new Uri($"/api/v1/reports/{reportId}?unit={unitId}", UriKind.Relative))
        {
            Content = JsonContent.Create(new SaveReportDraftRequest(answers)),
        };
        if (ifMatch is not null)
        {
            message.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch}\"");
        }

        return client.SendAsync(message);
    }

    private static async Task<Guid> SubmitMuqamAsync(HttpClient client, Guid windowId, Guid muqamId, Dictionary<string, object?> answers)
    {
        var saveResponse = await SaveAsync(client, windowId, muqamId, answers);
        var saved = await saveResponse.Content.ReadFromJsonAsync<SaveReportDraftResult>();
        await client.PostAsJsonAsync($"/api/v1/reports/{saved!.Id}/submit?unit={muqamId}", new SubmitReportRequest(Confirmed: true));
        return saved.Id;
    }

    private static Task<HttpResponseMessage> PullAsync(
        HttpClient client, Guid reportId, Guid unitId, IReadOnlyList<Guid>? includedUnitIds = null, string? overrideNote = null, string? ifMatch = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/v1/reports/{reportId}/pull?unit={unitId}", UriKind.Relative))
        {
            Content = JsonContent.Create(new PullChildFiguresRequest(includedUnitIds, overrideNote)),
        };
        if (ifMatch is not null)
        {
            message.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch}\"");
        }

        return client.SendAsync(message);
    }

    [Fact]
    public async Task PullContextIsEligibleWhenTheReportTypeAppliesAtBothLevels()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Pull Eligible Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamIds) = await CreateDilaWithMuqamaatAsync(secretariat, "01", childCount: 2);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var context = await (await dilaClient.GetAsync(new Uri($"/api/v1/reports/{windowId}/pull-context?unit={dilaId}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<PullContextView>();

        Assert.True(context!.Eligible);
        Assert.Equal("Muqam", context.ChildLevelName);
        Assert.Equal("Muqamaat", context.ChildLevelPluralName);
        Assert.Equal(2, context.Children.Count);
        Assert.All(muqamIds, id => Assert.Contains(context.Children, c => c.UnitId == id));
    }

    [Fact]
    public async Task MuqamHeadWithNoChildrenIsNotEligible()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Pull Muqam Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (_, muqamIds) = await CreateDilaWithMuqamaatAsync(secretariat, "02", childCount: 1);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);

        var (muqamClient, muqamUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(muqamUserId, muqamIds[0], RoleName.UnitHead);

        var context = await (await muqamClient.GetAsync(new Uri($"/api/v1/reports/{windowId}/pull-context?unit={muqamIds[0]}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<PullContextView>();

        Assert.False(context!.Eligible);
    }

    [Fact]
    public async Task PullComputesEveryAggregationRuleCorrectly()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Pull Aggregation Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamIds) = await CreateDilaWithMuqamaatAsync(secretariat, "03", childCount: 2);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);

        var (muqamAClient, muqamAUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(muqamAUserId, muqamIds[0], RoleName.UnitHead);
        await SubmitMuqamAsync(muqamAClient, windowId, muqamIds[0], new Dictionary<string, object?>
        {
            ["total_members"] = 100,
            ["avg_attendance"] = 40,
            ["max_score"] = 70,
            ["latest_value"] = 5,
            ["notes"] = "A",
        });

        var (muqamBClient, muqamBUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(muqamBUserId, muqamIds[1], RoleName.UnitHead);
        await SubmitMuqamAsync(muqamBClient, windowId, muqamIds[1], new Dictionary<string, object?>
        {
            ["total_members"] = 150,
            ["avg_attendance"] = 60,
            ["max_score"] = 90,
            ["latest_value"] = 9,
            ["notes"] = "B",
        });

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var response = await PullAsync(dilaClient, windowId, dilaId);
        var result = await response.Content.ReadFromJsonAsync<PullChildFiguresResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fields = result!.Summary.Fields.ToDictionary(f => f.FieldKey);
        Assert.Equal(250, fields["total_members"].Value);
        Assert.Equal(50, fields["avg_attendance"].Value);
        Assert.Equal(90, fields["max_score"].Value);
        Assert.False(fields.ContainsKey("notes"));
        Assert.Equal(2, result.Summary.IncludedUnitIds.Count);
        Assert.Empty(result.Summary.MissingUnitIds);

        // The pulled values must now be in the Dila's own draft.
        var editing = await (await dilaClient.GetAsync(new Uri($"/api/v1/reports/{windowId}?unit={dilaId}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<ReportInstanceForEditing>();
        var totalMembers = (JsonElement)editing!.Answers["total_members"]!;
        Assert.Equal(250, totalMembers.GetDouble());
    }

    [Fact]
    public async Task MissingChildrenAreListedAndExcludedFromTheAggregate()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Pull Missing Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamIds) = await CreateDilaWithMuqamaatAsync(secretariat, "04", childCount: 2);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);

        var (muqamAClient, muqamAUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(muqamAUserId, muqamIds[0], RoleName.UnitHead);
        await SubmitMuqamAsync(muqamAClient, windowId, muqamIds[0], new Dictionary<string, object?> { ["total_members"] = 100 });
        // muqamIds[1] never submits.

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var result = await (await PullAsync(dilaClient, windowId, dilaId)).Content.ReadFromJsonAsync<PullChildFiguresResult>();

        Assert.Single(result!.Summary.IncludedUnitIds);
        Assert.Single(result.Summary.MissingUnitIds);
        Assert.Equal(muqamIds[1], result.Summary.MissingUnitIds[0]);
        Assert.Equal(100, result.Summary.Fields.Single(f => f.FieldKey == "total_members").Value);
    }

    [Fact]
    public async Task DeselectingASubmittedChildExcludesItFromTheAggregate()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Pull Deselect Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamIds) = await CreateDilaWithMuqamaatAsync(secretariat, "05", childCount: 2);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);

        var (muqamAClient, muqamAUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(muqamAUserId, muqamIds[0], RoleName.UnitHead);
        await SubmitMuqamAsync(muqamAClient, windowId, muqamIds[0], new Dictionary<string, object?> { ["total_members"] = 100 });

        var (muqamBClient, muqamBUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(muqamBUserId, muqamIds[1], RoleName.UnitHead);
        await SubmitMuqamAsync(muqamBClient, windowId, muqamIds[1], new Dictionary<string, object?> { ["total_members"] = 150 });

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var result = await (await PullAsync(dilaClient, windowId, dilaId, includedUnitIds: [muqamIds[0]]))
            .Content.ReadFromJsonAsync<PullChildFiguresResult>();

        Assert.Equal(100, result!.Summary.Fields.Single(f => f.FieldKey == "total_members").Value);
        Assert.Single(result.Summary.IncludedUnitIds);
    }

    [Fact]
    public async Task RepullingPreservesAManuallyOverriddenFieldButRefreshesTheRest()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Pull Repull Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamIds) = await CreateDilaWithMuqamaatAsync(secretariat, "06", childCount: 1);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);

        var (muqamClient, muqamUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(muqamUserId, muqamIds[0], RoleName.UnitHead);
        await SubmitMuqamAsync(muqamClient, windowId, muqamIds[0], new Dictionary<string, object?> { ["total_members"] = 100, ["avg_attendance"] = 40 });

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        var firstPull = await (await PullAsync(dilaClient, windowId, dilaId)).Content.ReadFromJsonAsync<PullChildFiguresResult>();
        Assert.Equal(100, firstPull!.Summary.Fields.Single(f => f.FieldKey == "total_members").Value);

        // The head manually overrides total_members via a normal save, leaving avg_attendance untouched.
        var overrideSave = await SaveAsync(
            dilaClient, firstPull.Id, dilaId,
            new Dictionary<string, object?> { ["total_members"] = 999, ["avg_attendance"] = 40 }, firstPull.RowVersion);
        var overrideSaved = await overrideSave.Content.ReadFromJsonAsync<SaveReportDraftResult>();

        var secondPull = await (await PullAsync(dilaClient, windowId, dilaId, ifMatch: overrideSaved!.RowVersion))
            .Content.ReadFromJsonAsync<PullChildFiguresResult>();

        var totalMembersField = secondPull!.Summary.Fields.Single(f => f.FieldKey == "total_members");
        var avgAttendanceField = secondPull.Summary.Fields.Single(f => f.FieldKey == "avg_attendance");
        Assert.True(totalMembersField.IsOverridden);
        Assert.Equal(100, totalMembersField.Value); // the freshly computed pull value, for reference
        Assert.False(avgAttendanceField.IsOverridden);

        var editing = await (await dilaClient.GetAsync(new Uri($"/api/v1/reports/{windowId}?unit={dilaId}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<ReportInstanceForEditing>();
        var totalMembers = (JsonElement)editing!.Answers["total_members"]!;
        Assert.Equal(999, totalMembers.GetDouble()); // untouched by the second pull
    }

    [Fact]
    public async Task NonEditorCannotPullOrSeeContext()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Pull Forbidden Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, _) = await CreateDilaWithMuqamaatAsync(secretariat, "07", childCount: 1);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);

        var (client, _) = await CreateAuthenticatedUserAsync();

        var contextResponse = await client.GetAsync(new Uri($"/api/v1/reports/{windowId}/pull-context?unit={dilaId}", UriKind.Relative));
        var pullResponse = await PullAsync(client, windowId, dilaId);

        Assert.Equal(HttpStatusCode.Forbidden, contextResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, pullResponse.StatusCode);
    }

    [Fact]
    public async Task RepullingAnExistingInstanceWithoutIfMatchFails()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Pull NoIfMatch Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var (dilaId, muqamIds) = await CreateDilaWithMuqamaatAsync(secretariat, "08", childCount: 1);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);

        var (muqamClient, muqamUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(muqamUserId, muqamIds[0], RoleName.UnitHead);
        await SubmitMuqamAsync(muqamClient, windowId, muqamIds[0], new Dictionary<string, object?> { ["total_members"] = 100 });

        var (dilaClient, dilaUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaUserId, dilaId, RoleName.UnitHead);

        await PullAsync(dilaClient, windowId, dilaId);
        var secondResponse = await PullAsync(dilaClient, windowId, dilaId);

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }
}
