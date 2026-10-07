using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Templates.ReportTypes;
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
}
