using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Review.Department;
using HRS.Application.Features.Submissions.ReportInstances;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;

namespace HRS.Api.IntegrationTests;

/// <summary>FR-REV-005 (M7.4). "DR"-prefixed unit codes — see InboxEndpointsTests's own note on
/// why every file in this collection needs its own prefix.</summary>
[Collection(ApiTestCollection.Name)]
public class DepartmentReportsEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Dept1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedUserAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Dept2";
        var userId = await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, userId);
    }

    private static async Task<Guid> CreateDepartmentUnitAsync(HttpClient client, string suffix, string name)
    {
        var created = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"DRDEPT{suffix}", name, OrganizationSeedIds.DepartmentLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        return created!.Id;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateDeptOfficerAsync(Guid departmentUnitId)
    {
        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, departmentUnitId, RoleName.DeptOfficer);
        return (client, userId);
    }

    private static async Task<Guid> CreateReportTypeAsync(HttpClient client, string name)
    {
        var created = await (await client.PostAsJsonAsync(
                "/api/v1/report-types",
                new CreateReportTypeRequest(
                    name, "Monthly activity figures", ReportTypeCategory.GeneralMonthly, ReportTypeFrequency.Monthly,
                    [UnitLevelCode.Muqam, UnitLevelCode.Dila], UnitLevelCode.National, 1, AllowAttachments: false, AllowNilReport: false)))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        return created!.Id;
    }

    private static async Task PublishTemplateAsync(HttpClient client, Guid reportTypeId, IReadOnlyList<TemplateField> fields)
    {
        var schema = new TemplateSchema([new TemplateSection("section_1", "Figures", fields)]);
        var draft = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(schema)))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);
    }

    private static TemplateField Field(
        string key, string label, Guid? owningDepartmentId, bool confidential = false, IReadOnlyList<UnitLevelCode>? visibleForLevels = null) =>
        new(key, TemplateFieldType.ShortText, label, null, false, null, null, null, null, null, confidential,
            owningDepartmentId, visibleForLevels, null, AggregationRule.None, null, null, null);

    private static async Task<Guid> CreateUnitAsync(HttpClient client, string suffix, Guid levelId, Guid parentId, string name) =>
        (await (await client.PostAsJsonAsync(
                "/api/v1/units", new CreateUnitRequest($"DR{suffix}", name, levelId, parentId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>())!.Id;

    /// <summary>Dila MUST be a child of Zone (UnitHierarchyRules) — returns both, since several
    /// tests need a Dila-level unit to submit at directly, not just the Muqam underneath it.</summary>
    private static async Task<(Guid ZoneId, Guid DilaId)> CreateZoneAndDilaUnitAsync(HttpClient client, string suffix)
    {
        var zone = await CreateUnitAsync(client, $"Z{suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, $"Dept Zone {suffix}");
        var dila = await CreateUnitAsync(client, $"D{suffix}", OrganizationSeedIds.DilaLevelId, zone, $"Dept Dila {suffix}");
        return (zone, dila);
    }

    private static async Task<Guid> CreateMuqamUnitAsync(HttpClient client, string suffix)
    {
        var (_, dila) = await CreateZoneAndDilaUnitAsync(client, suffix);
        return await CreateUnitAsync(client, $"M{suffix}", OrganizationSeedIds.MuqamLevelId, dila, $"Dept Muqam {suffix}");
    }

    private static async Task<Guid> CreateWindowAsync(HttpClient client, Guid reportTypeId, string periodLabel = "Sep 2026")
    {
        var request = new CreateSubmissionWindowRequest(
            periodLabel, Now.AddDays(-10), 3, Now.AddDays(20),
            [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10)), new WindowDueDate(UnitLevelCode.Dila, Now.AddDays(10))]);
        var created = await (await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();
        return created!.Window.Id;
    }

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, Guid reportId, Guid unitId, Dictionary<string, object?> answers) =>
        client.PutAsJsonAsync($"/api/v1/reports/{reportId}?unit={unitId}", new SaveReportDraftRequest(answers));

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid reportId, Guid unitId) =>
        client.PostAsJsonAsync($"/api/v1/reports/{reportId}/submit?unit={unitId}", new SubmitReportRequest(Confirmed: true));

    private async Task SubmitReportAsync(Guid windowId, Guid unitId, Dictionary<string, object?> answers)
    {
        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, unitId, RoleName.UnitHead);
        var saved = await (await SaveAsync(client, windowId, unitId, answers)).Content.ReadFromJsonAsync<SaveReportDraftResult>();
        await SubmitAsync(client, saved!.Id, unitId);
    }

    private static Task<HttpResponseMessage> GetDepartmentReportsAsync(HttpClient client, string query = "") =>
        client.GetAsync(new Uri($"/api/v1/department/reports{query}", UriKind.Relative));

    [Fact]
    public async Task OfficerSeesOnlyTheirDepartmentsFieldsAcrossUnitsAndLevels()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var financeDeptId = await CreateDepartmentUnitAsync(secretariat, "01", "Finance");
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Dept Multi-Level Report");
        await PublishTemplateAsync(secretariat, reportTypeId, [
            Field("income", "Income", financeDeptId),
            Field("notes", "Notes", owningDepartmentId: null),
        ]);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);

        var muqamId = await CreateMuqamUnitAsync(secretariat, "01");
        var (_, dilaId) = await CreateZoneAndDilaUnitAsync(secretariat, "01x");
        await SubmitReportAsync(windowId, muqamId, new Dictionary<string, object?> { ["income"] = "500", ["notes"] = "secret plan" });
        await SubmitReportAsync(windowId, dilaId, new Dictionary<string, object?> { ["income"] = "900", ["notes"] = "another plan" });

        var (officerClient, _) = await CreateDeptOfficerAsync(financeDeptId);
        var rows = await (await GetDepartmentReportsAsync(officerClient)).Content.ReadFromJsonAsync<List<DepartmentReportRow>>();

        Assert.Equal(2, rows!.Count);
        foreach (var row in rows)
        {
            Assert.Single(row.Fields);
            Assert.Equal("income", row.Fields[0].FieldKey);
            Assert.DoesNotContain(row.Fields, f => f.FieldKey == "notes");
        }

        var muqamRow = rows.Single(r => r.UnitId == muqamId);
        Assert.Equal("500", muqamRow.Fields[0].Value);
    }

    [Fact]
    public async Task ValuesAreNotMaskedEvenWhenTheFieldIsConfidential()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var financeDeptId = await CreateDepartmentUnitAsync(secretariat, "02", "Finance");
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Dept Confidential Report");
        await PublishTemplateAsync(secretariat, reportTypeId, [Field("salary", "Salary", financeDeptId, confidential: true)]);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "02");
        await SubmitReportAsync(windowId, muqamId, new Dictionary<string, object?> { ["salary"] = "12345" });

        var (officerClient, _) = await CreateDeptOfficerAsync(financeDeptId);
        var rows = await (await GetDepartmentReportsAsync(officerClient)).Content.ReadFromJsonAsync<List<DepartmentReportRow>>();

        Assert.Equal("12345", rows!.Single().Fields.Single().Value);
    }

    [Fact]
    public async Task AFieldNotVisibleAtThisUnitLevelIsExcludedAndTheRowDisappearsIfItWasTheOnlyOwnedField()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var financeDeptId = await CreateDepartmentUnitAsync(secretariat, "03", "Finance");
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Dept Level Visibility Report");
        await PublishTemplateAsync(secretariat, reportTypeId, [Field("dila_only_figure", "Dila-only figure", financeDeptId, visibleForLevels: [UnitLevelCode.Dila])]);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "03");
        await SubmitReportAsync(windowId, muqamId, new Dictionary<string, object?> { ["dila_only_figure"] = "x" });

        var (officerClient, _) = await CreateDeptOfficerAsync(financeDeptId);
        var rows = await (await GetDepartmentReportsAsync(officerClient)).Content.ReadFromJsonAsync<List<DepartmentReportRow>>();

        Assert.Empty(rows!);
    }

    [Fact]
    public async Task DraftReportsNeverSubmittedProduceNoRow()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var financeDeptId = await CreateDepartmentUnitAsync(secretariat, "04", "Finance");
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Dept Draft Exclusion Report");
        await PublishTemplateAsync(secretariat, reportTypeId, [Field("income", "Income", financeDeptId)]);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "04");

        var (draftClient, draftUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(draftUserId, muqamId, RoleName.UnitHead);
        await SaveAsync(draftClient, windowId, muqamId, new Dictionary<string, object?> { ["income"] = "draft only" });

        var (officerClient, _) = await CreateDeptOfficerAsync(financeDeptId);
        var rows = await (await GetDepartmentReportsAsync(officerClient)).Content.ReadFromJsonAsync<List<DepartmentReportRow>>();

        Assert.Empty(rows!);
    }

    [Fact]
    public async Task FiltersByWindowReportTypeAndLevel()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var financeDeptId = await CreateDepartmentUnitAsync(secretariat, "05", "Finance");
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Dept Filter Report");
        await PublishTemplateAsync(secretariat, reportTypeId, [Field("income", "Income", financeDeptId)]);
        var augWindowId = await CreateWindowAsync(secretariat, reportTypeId, "Aug 2026");
        var sepWindowId = await CreateWindowAsync(secretariat, reportTypeId, "Sep 2026");

        var muqamId = await CreateMuqamUnitAsync(secretariat, "05");
        var (_, dilaId) = await CreateZoneAndDilaUnitAsync(secretariat, "05x");
        await SubmitReportAsync(augWindowId, muqamId, new Dictionary<string, object?> { ["income"] = "aug-muqam" });
        await SubmitReportAsync(sepWindowId, dilaId, new Dictionary<string, object?> { ["income"] = "sep-dila" });

        var (officerClient, _) = await CreateDeptOfficerAsync(financeDeptId);

        var byWindow = await (await GetDepartmentReportsAsync(officerClient, $"?windowId={sepWindowId}")).Content.ReadFromJsonAsync<List<DepartmentReportRow>>();
        Assert.Single(byWindow!);
        Assert.Equal("sep-dila", byWindow![0].Fields[0].Value);

        var byLevel = await (await GetDepartmentReportsAsync(officerClient, $"?unitLevel={(int)UnitLevelCode.Muqam}")).Content.ReadFromJsonAsync<List<DepartmentReportRow>>();
        Assert.Single(byLevel!);
        Assert.Equal("aug-muqam", byLevel![0].Fields[0].Value);

        var byReportType = await (await GetDepartmentReportsAsync(officerClient, $"?reportTypeId={reportTypeId}")).Content.ReadFromJsonAsync<List<DepartmentReportRow>>();
        Assert.Equal(2, byReportType!.Count);
    }

    [Fact]
    public async Task IsForbiddenWithoutAnActiveDeptOfficerAssignment()
    {
        var (client, _) = await CreateAuthenticatedUserAsync();

        var response = await GetDepartmentReportsAsync(client);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
