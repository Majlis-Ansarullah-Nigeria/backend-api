using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class WindowsEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow.AddDays(1);

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
        const string password = "Correct-Horse-Win1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<HttpClient> CreatePlainUserClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Win2";
        await factory.CreateUserAsync(email, password);

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

    private static CreateSubmissionWindowRequest ValidWindow(params WindowDueDate[] dueDates) => new(
        "October 2026",
        Now,
        3,
        Now.AddDays(30),
        dueDates);

    [Fact]
    public async Task CreatingAWindowRequiresSecretariat()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Gate Report", UnitLevelCode.Muqam);

        using var client = await CreatePlainUserClientAsync();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows",
            ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SecretariatCanCreateAWindow()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Valid Window Report", UnitLevelCode.Muqam);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows",
            ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();
        Assert.Equal("October 2026", result!.Window.PeriodLabel);
        Assert.Single(result.Window.DueDates);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task CreatingAWindowForAnUnknownReportTypeReturnsNotFound()
    {
        using var client = await CreateSecretariatClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{Guid.NewGuid()}/windows",
            ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreatingAWindowForAnArchivedReportTypeFails()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Archived Window Report", UnitLevelCode.Muqam);
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/archive", UriKind.Relative), null);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows",
            ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ADueDateForANonApplicableLevelFails()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Muqam Only Report", UnitLevelCode.Muqam);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows",
            ValidWindow(new WindowDueDate(UnitLevelCode.Zone, Now.AddDays(10))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AParentDueBeforeItsChildProducesAWarningButStillSaves()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Warning Report", UnitLevelCode.Muqam, UnitLevelCode.Dila);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows",
            ValidWindow(
                new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10)),
                new WindowDueDate(UnitLevelCode.Dila, Now.AddDays(5))));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();
        Assert.Single(result!.Warnings);
    }

    [Fact]
    public async Task GetSubmissionWindowReturnsTheCreatedWindow()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Fetchable Window Report", UnitLevelCode.Muqam);
        var created = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/windows",
                ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10)))))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        var response = await client.GetAsync(
            new Uri($"/api/v1/report-types/{reportTypeId}/windows/{created!.Window.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSubmissionWindowForAnUnknownIdReturnsNotFound()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Unknown Window Report", UnitLevelCode.Muqam);

        var response = await client.GetAsync(
            new Uri($"/api/v1/report-types/{reportTypeId}/windows/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSubmissionWindowsListsWindowsForTheReportTypeOnly()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Listed Window Report", UnitLevelCode.Muqam);
        var otherReportTypeId = await CreateReportTypeAsync(client, "Other Window Report", UnitLevelCode.Muqam);
        await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/windows", ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))));
        await client.PostAsJsonAsync(
            $"/api/v1/report-types/{otherReportTypeId}/windows", ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))));

        var response = await client.GetAsync(new Uri($"/api/v1/report-types/{reportTypeId}/windows", UriKind.Relative));
        var windows = await response.Content.ReadFromJsonAsync<List<SubmissionWindowSummary>>();

        var window = Assert.Single(windows!);
        Assert.Equal(reportTypeId, window.ReportTypeId);
    }

    [Fact]
    public async Task UpdateSubmissionWindowReplacesItsDueDates()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Updatable Window Report", UnitLevelCode.Muqam, UnitLevelCode.Dila);
        var created = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/windows",
                ValidWindow(new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10)))))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        var update = new UpdateSubmissionWindowRequest(
            "November 2026", Now.AddDays(1), 5, Now.AddDays(40), [new WindowDueDate(UnitLevelCode.Dila, Now.AddDays(15))]);
        var response = await client.PutAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows/{created!.Window.Id}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();
        Assert.Equal("November 2026", result!.Window.PeriodLabel);
        var dueDate = Assert.Single(result.Window.DueDates);
        Assert.Equal(UnitLevelCode.Dila, dueDate.Level);
    }

    [Fact]
    public async Task UpdateSubmissionWindowForAnUnknownIdReturnsNotFound()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Unknown Update Window Report", UnitLevelCode.Muqam);

        var update = new UpdateSubmissionWindowRequest(
            "November 2026", Now, 3, Now.AddDays(30), [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))]);
        var response = await client.PutAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows/{Guid.NewGuid()}", update);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
