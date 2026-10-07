using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class TemplatesEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static CreateReportTypeRequest MonthlyMuqamReport(string name = "General Monthly Report") => new(
        name,
        "Monthly activity figures",
        ReportTypeCategory.GeneralMonthly,
        ReportTypeFrequency.Monthly,
        [UnitLevelCode.Muqam],
        UnitLevelCode.National,
        1,
        AllowAttachments: true);

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Tpl1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<HttpClient> CreateSysAdminClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Tpl2";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.SysAdmin);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<HttpClient> CreatePlainUserClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Tpl3";
        await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    [Fact]
    public async Task CreatingAReportTypeRequiresSecretariat()
    {
        using var client = await CreatePlainUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/report-types", MonthlyMuqamReport());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SysAdminCannotCreateAReportType()
    {
        using var client = await CreateSysAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/report-types", MonthlyMuqamReport());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SecretariatCanCreateAReportType()
    {
        using var client = await CreateSecretariatClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/report-types", MonthlyMuqamReport("Monthly Return"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reportType = await response.Content.ReadFromJsonAsync<ReportTypeSummary>();
        Assert.Equal("Monthly Return", reportType!.Name);
        Assert.Equal(ReportTypeCategory.GeneralMonthly, reportType.Category);
        Assert.Equal(ReportTypeFrequency.Monthly, reportType.Frequency);
        Assert.Equal([UnitLevelCode.Muqam], reportType.ApplicableLevels);
        Assert.Equal(UnitLevelCode.National, reportType.RecipientLevel);
        Assert.Equal(1, reportType.ObserverDepth);
        Assert.True(reportType.AllowAttachments);
        Assert.Null(reportType.ArchivedAt);
    }

    [Fact]
    public async Task CreatingAReportTypeWithNoApplicableLevelsFails()
    {
        using var client = await CreateSecretariatClientAsync();
        var request = new CreateReportTypeRequest(
            "Empty Levels", null, ReportTypeCategory.AdHoc, ReportTypeFrequency.AdHoc, [], UnitLevelCode.National, 0, false);

        var response = await client.PostAsJsonAsync("/api/v1/report-types", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetReportTypeReturnsTheCreatedReportType()
    {
        using var client = await CreateSecretariatClientAsync();
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MonthlyMuqamReport("Fetchable Report")))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();

        var response = await client.GetAsync(new Uri($"/api/v1/report-types/{created!.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reportType = await response.Content.ReadFromJsonAsync<ReportTypeSummary>();
        Assert.Equal("Fetchable Report", reportType!.Name);
    }

    [Fact]
    public async Task GetReportTypeForAnUnknownIdReturnsNotFound()
    {
        using var client = await CreateSecretariatClientAsync();

        var response = await client.GetAsync(new Uri($"/api/v1/report-types/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetReportTypesListsOnlyActiveByDefault()
    {
        using var client = await CreateSecretariatClientAsync();
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MonthlyMuqamReport("Archivable Report")))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        await client.PostAsync(new Uri($"/api/v1/report-types/{created!.Id}/archive", UriKind.Relative), null);

        var defaultList = await (await client.GetAsync(new Uri("/api/v1/report-types", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<ReportTypeSummary>>();
        Assert.DoesNotContain(defaultList!, r => r.Id == created.Id);

        var includeArchivedList = await (await client.GetAsync(new Uri("/api/v1/report-types?includeArchived=true", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<ReportTypeSummary>>();
        Assert.Contains(includeArchivedList!, r => r.Id == created.Id);
    }

    [Fact]
    public async Task UpdateReportTypeChangesItsFields()
    {
        using var client = await CreateSecretariatClientAsync();
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MonthlyMuqamReport("Original Name")))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();

        var update = new UpdateReportTypeRequest(
            "Renamed Report",
            "Updated description",
            ReportTypeCategory.Quarterly,
            ReportTypeFrequency.Quarterly,
            [UnitLevelCode.Dila, UnitLevelCode.Zone],
            UnitLevelCode.National,
            2,
            false);
        var response = await client.PutAsJsonAsync($"/api/v1/report-types/{created!.Id}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ReportTypeSummary>();
        Assert.Equal("Renamed Report", updated!.Name);
        Assert.Equal(ReportTypeCategory.Quarterly, updated.Category);
        Assert.Equal(new[] { UnitLevelCode.Dila, UnitLevelCode.Zone }, updated.ApplicableLevels);
        Assert.Equal(2, updated.ObserverDepth);
        Assert.False(updated.AllowAttachments);
    }

    [Fact]
    public async Task ArchiveThenRestoreRoundTrips()
    {
        using var client = await CreateSecretariatClientAsync();
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MonthlyMuqamReport("Round Trip Report")))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();

        var archive = await client.PostAsync(new Uri($"/api/v1/report-types/{created!.Id}/archive", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var afterArchive = await (await client.GetAsync(new Uri($"/api/v1/report-types/{created.Id}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        Assert.NotNull(afterArchive!.ArchivedAt);

        var restore = await client.PostAsync(new Uri($"/api/v1/report-types/{created.Id}/restore", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.NoContent, restore.StatusCode);

        var afterRestore = await (await client.GetAsync(new Uri($"/api/v1/report-types/{created.Id}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        Assert.Null(afterRestore!.ArchivedAt);
    }

    private static TemplateField NumberField(string key = "total_members") => new(
        key, TemplateFieldType.Number, "Total members", null, true, 0, null, null, null, "members", false,
        null, null, null, AggregationRule.Sum, null, null, null);

    private static TemplateSchema OneFieldSchema() =>
        new([new TemplateSection("membership", "Membership", [NumberField()])]);

    private static async Task<Guid> CreateReportTypeAsync(HttpClient client, string name)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MonthlyMuqamReport(name)))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        return created!.Id;
    }

    [Fact]
    public async Task UpdatingTemplateSchemaRequiresSecretariat()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Gate Report");

        using var client = await CreatePlainUserClientAsync();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(OneFieldSchema()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FirstEditCreatesVersion1AsDraft()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "V1 Report");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(OneFieldSchema()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<TemplateVersionSummary>();
        Assert.Equal(1, summary!.VersionNumber);
        Assert.Equal(TemplateVersionStatus.Draft, summary.Status);
    }

    [Fact]
    public async Task EditingAnOpenDraftUpdatesItInPlace()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "In Place Report");
        var first = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(OneFieldSchema())))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();

        var editedSchema = new TemplateSchema([new TemplateSection("membership", "Membership", [NumberField("new_members")])]);
        var second = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(editedSchema)))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();

        Assert.Equal(first!.Id, second!.Id);
        Assert.Equal(1, second.VersionNumber);

        var detail = await (await client.GetAsync(
                new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{second.Id}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<TemplateVersionDetail>();
        Assert.Equal("new_members", detail!.Schema.Sections[0].Fields[0].Key);
    }

    [Fact]
    public async Task EditingAfterPublishForksANewDraft()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Fork Report");
        var v1 = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(OneFieldSchema())))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{v1!.Id}/publish", UriKind.Relative), null);

        var v2 = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(OneFieldSchema())))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();

        Assert.NotEqual(v1.Id, v2!.Id);
        Assert.Equal(2, v2.VersionNumber);
        Assert.Equal(TemplateVersionStatus.Draft, v2.Status);

        var versions = await (await client.GetAsync(
                new Uri($"/api/v1/report-types/{reportTypeId}/template-versions", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<TemplateVersionSummary>>();
        Assert.Equal(TemplateVersionStatus.Published, versions!.Single(v => v.Id == v1.Id).Status);
    }

    [Fact]
    public async Task PublishingASecondDraftRetiresThePreviouslyPublishedVersion()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Retire Report");
        var v1 = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(OneFieldSchema())))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{v1!.Id}/publish", UriKind.Relative), null);
        var v2 = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(OneFieldSchema())))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();

        var publishV2 = await client.PostAsync(
            new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{v2!.Id}/publish", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.OK, publishV2.StatusCode);
        var versions = await (await client.GetAsync(
                new Uri($"/api/v1/report-types/{reportTypeId}/template-versions", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<TemplateVersionSummary>>();
        Assert.Equal(TemplateVersionStatus.Retired, versions!.Single(v => v.Id == v1.Id).Status);
        Assert.Equal(TemplateVersionStatus.Published, versions!.Single(v => v.Id == v2.Id).Status);
    }

    [Fact]
    public async Task PublishingRequiresSecretariat()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Publish Gate Report");
        var draft = await (await secretariat.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(OneFieldSchema())))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();

        using var client = await CreatePlainUserClientAsync();
        var response = await client.PostAsync(
            new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PublishingANonDraftVersionFails()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Already Published Report");
        var v1 = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(OneFieldSchema())))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{v1!.Id}/publish", UriKind.Relative), null);

        var response = await client.PostAsync(
            new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{v1.Id}/publish", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PublishingAnUnknownVersionReturnsNotFound()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Unknown Version Report");

        var response = await client.PostAsync(
            new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{Guid.NewGuid()}/publish", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTemplateVersionsForAnUnknownReportTypeReturnsNotFound()
    {
        using var client = await CreateSecretariatClientAsync();

        var response = await client.GetAsync(
            new Uri($"/api/v1/report-types/{Guid.NewGuid()}/template-versions", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTemplateVersionForAnUnknownIdReturnsNotFound()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Unknown Detail Report");

        var response = await client.GetAsync(
            new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EditingATemplateForAnArchivedReportTypeFails()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Archived Template Report");
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/archive", UriKind.Relative), null);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(OneFieldSchema()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task EditingATemplateForAnUnknownReportTypeReturnsNotFound()
    {
        using var client = await CreateSecretariatClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{Guid.NewGuid()}/template-versions", new UpdateTemplateSchemaRequest(OneFieldSchema()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SchemaValidationRejectsDuplicateFieldKeysAcrossSections()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Duplicate Keys Report");
        var schema = new TemplateSchema([
            new TemplateSection("s1", "Section 1", [NumberField("members")]),
            new TemplateSection("s2", "Section 2", [NumberField("members")]),
        ]);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(schema));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SchemaValidationRejectsAChoiceFieldWithNoOptions()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "No Options Report");
        var field = new TemplateField(
            "fav_color", TemplateFieldType.Dropdown, "Favorite color", null, false, null, null, null, null, null,
            false, null, null, null, AggregationRule.None, null, null, null);
        var schema = new TemplateSchema([new TemplateSection("prefs", "Preferences", [field])]);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(schema));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTemplateVersionRoundTripsADropdownFieldWithOptions()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Round Trip Options Report");
        var field = new TemplateField(
            "fav_color", TemplateFieldType.Dropdown, "Favorite color", "Pick one", false, null, null, null, null, null,
            false, null, null, null, AggregationRule.None, null,
            [new TemplateFieldOption("red", "Red"), new TemplateFieldOption("blue", "Blue")], null);
        var schema = new TemplateSchema([new TemplateSection("prefs", "Preferences", [field])]);
        var created = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(schema)))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();

        var detail = await (await client.GetAsync(
                new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{created!.Id}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<TemplateVersionDetail>();

        var roundTrippedField = detail!.Schema.Sections[0].Fields[0];
        Assert.Equal("fav_color", roundTrippedField.Key);
        Assert.Equal(2, roundTrippedField.Options!.Count);
        Assert.Contains(roundTrippedField.Options, o => o.Value == "red" && o.Label == "Red");
    }
}
