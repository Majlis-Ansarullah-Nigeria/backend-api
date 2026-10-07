using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Windows.WindowSchedules;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class WindowSchedulesEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

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
        const string password = "Correct-Horse-Sched1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<HttpClient> CreatePlainUserClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Sched2";
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

    private static CreateWindowScheduleRequest MonthlySchedule(params ScheduleDueOffset[] dueOffsets) => new(
        ReportTypeFrequency.Monthly, 1, 3, 25, dueOffsets);

    [Fact]
    public async Task CreatingAScheduleRequiresSecretariat()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Gate Schedule Report", UnitLevelCode.Muqam);

        using var client = await CreatePlainUserClientAsync();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/window-schedules",
            MonthlySchedule(new ScheduleDueOffset(UnitLevelCode.Muqam, 10)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreatingAMonthlyScheduleGenerates12Windows()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Monthly Schedule Report", UnitLevelCode.Muqam);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/window-schedules",
            MonthlySchedule(new ScheduleDueOffset(UnitLevelCode.Muqam, 10)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateWindowScheduleResult>();
        Assert.Equal(12, result!.WindowsGenerated);

        var windows = await (await client.GetAsync(new Uri($"/api/v1/report-types/{reportTypeId}/windows", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<object>>();
        Assert.Equal(12, windows!.Count);
    }

    [Fact]
    public async Task CreatingAQuarterlyScheduleGenerates4Windows()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Quarterly Schedule Report", UnitLevelCode.Zone);

        var request = new CreateWindowScheduleRequest(
            ReportTypeFrequency.Quarterly, 1, 3, 20, [new ScheduleDueOffset(UnitLevelCode.Zone, 10)]);
        var response = await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/window-schedules", request);

        var result = await response.Content.ReadFromJsonAsync<CreateWindowScheduleResult>();
        Assert.Equal(4, result!.WindowsGenerated);
    }

    [Fact]
    public async Task GeneratingAgainDoesNotDuplicateWindows()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Idempotent Schedule Report", UnitLevelCode.Muqam);
        var created = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/window-schedules",
                MonthlySchedule(new ScheduleDueOffset(UnitLevelCode.Muqam, 10))))
            .Content.ReadFromJsonAsync<CreateWindowScheduleResult>();

        var response = await client.PostAsync(
            new Uri($"/api/v1/report-types/{reportTypeId}/window-schedules/{created!.Schedule.Id}/generate", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        Assert.Equal(0, body!["windowsGenerated"]);

        var windows = await (await client.GetAsync(new Uri($"/api/v1/report-types/{reportTypeId}/windows", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<object>>();
        Assert.Equal(12, windows!.Count);
    }

    [Fact]
    public async Task ADueOffsetForANonApplicableLevelFails()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Mismatched Schedule Report", UnitLevelCode.Muqam);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/window-schedules",
            MonthlySchedule(new ScheduleDueOffset(UnitLevelCode.Zone, 10)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeactivatingAScheduleStopsFurtherGeneration()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Deactivatable Schedule Report", UnitLevelCode.Muqam);
        var created = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/window-schedules",
                MonthlySchedule(new ScheduleDueOffset(UnitLevelCode.Muqam, 10))))
            .Content.ReadFromJsonAsync<CreateWindowScheduleResult>();

        var deactivate = await client.PostAsync(
            new Uri($"/api/v1/report-types/{reportTypeId}/window-schedules/{created!.Schedule.Id}/deactivate", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        var generate = await client.PostAsync(
            new Uri($"/api/v1/report-types/{reportTypeId}/window-schedules/{created.Schedule.Id}/generate", UriKind.Relative), null);
        var body = await generate.Content.ReadFromJsonAsync<Dictionary<string, int>>();

        Assert.Equal(0, body!["windowsGenerated"]);
    }

    [Fact]
    public async Task ReactivatingAScheduleResumesGeneration()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Reactivatable Schedule Report", UnitLevelCode.Muqam);
        var created = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/window-schedules",
                MonthlySchedule(new ScheduleDueOffset(UnitLevelCode.Muqam, 10))))
            .Content.ReadFromJsonAsync<CreateWindowScheduleResult>();
        await client.PostAsync(
            new Uri($"/api/v1/report-types/{reportTypeId}/window-schedules/{created!.Schedule.Id}/deactivate", UriKind.Relative), null);

        var response = await client.PostAsync(
            new Uri($"/api/v1/report-types/{reportTypeId}/window-schedules/{created.Schedule.Id}/activate", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateWindowScheduleResult>();
        Assert.True(result!.Schedule.IsActive);
    }

    [Fact]
    public async Task UpdateScheduleChangesItsFieldsAndRegenerates()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Updatable Schedule Report", UnitLevelCode.Muqam);
        var created = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/window-schedules",
                MonthlySchedule(new ScheduleDueOffset(UnitLevelCode.Muqam, 10))))
            .Content.ReadFromJsonAsync<CreateWindowScheduleResult>();

        var update = new UpdateWindowScheduleRequest(ReportTypeFrequency.Monthly, 2, 5, 28, [new ScheduleDueOffset(UnitLevelCode.Muqam, 12)]);
        var response = await client.PutAsJsonAsync($"/api/v1/report-types/{reportTypeId}/window-schedules/{created!.Schedule.Id}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateWindowScheduleResult>();
        Assert.Equal(2, result!.Schedule.OpensDayOffset);
        Assert.Equal(5, result.Schedule.GraceDays);
    }

    [Fact]
    public async Task GetWindowSchedulesListsSchedulesForTheReportTypeOnly()
    {
        using var client = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(client, "Listed Schedule Report", UnitLevelCode.Muqam);
        var otherReportTypeId = await CreateReportTypeAsync(client, "Other Listed Schedule Report", UnitLevelCode.Muqam);
        await client.PostAsJsonAsync(
            $"/api/v1/report-types/{reportTypeId}/window-schedules", MonthlySchedule(new ScheduleDueOffset(UnitLevelCode.Muqam, 10)));
        await client.PostAsJsonAsync(
            $"/api/v1/report-types/{otherReportTypeId}/window-schedules", MonthlySchedule(new ScheduleDueOffset(UnitLevelCode.Muqam, 10)));

        var response = await client.GetAsync(new Uri($"/api/v1/report-types/{reportTypeId}/window-schedules", UriKind.Relative));
        var schedules = await response.Content.ReadFromJsonAsync<List<WindowScheduleSummary>>();

        var schedule = Assert.Single(schedules!);
        Assert.Equal(reportTypeId, schedule.ReportTypeId);
    }
}
