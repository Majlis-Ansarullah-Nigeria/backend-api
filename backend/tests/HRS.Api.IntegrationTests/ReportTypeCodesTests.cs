using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Submissions.Review;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Api.IntegrationTests;

/// <summary>
/// FR-SUB-010: reference numbers start with the report type's own code, so two monthly report
/// types for one unit in one month no longer collide (that used to fail the second submission
/// with a 500).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class ReportTypeCodesTests(ApiFactory factory)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private async Task<(HttpClient Client, Guid UserId)> SignInAsync(RoleName? role)
    {
        var email = $"{Guid.NewGuid():N}@example.org";
        const string password = "Correct-Horse-Codes1";
        var userId = await factory.CreateUserAsync(email, password);
        if (role is { } r)
        {
            await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, r);
        }

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        return (client, userId);
    }

    private static CreateReportTypeRequest Monthly(string name, string? code = null) =>
        new(name, null, ReportTypeCategory.GeneralMonthly, ReportTypeFrequency.Monthly,
            [UnitLevelCode.Dila], UnitLevelCode.National, ObserverDepth: 1, AllowAttachments: false, AllowNilReport: false, Code: code);

    private static async Task<ReportTypeSummary> CreateTypeAsync(HttpClient secretariat, CreateReportTypeRequest request)
    {
        var response = await secretariat.PostAsJsonAsync("/api/v1/report-types", request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var type = (await response.Content.ReadFromJsonAsync<ReportTypeSummary>())!;

        var schema = new TemplateSchema(
        [
            new TemplateSection("figures", "Figures",
            [
                new TemplateField("members", TemplateFieldType.Number, "Members", null, false, null, null, null, null, null, false,
                    null, null, null, AggregationRule.Sum, null, null, null),
            ]),
        ]);
        var draft = await (await secretariat.PostAsJsonAsync($"/api/v1/report-types/{type.Id}/template-versions", new UpdateTemplateSchemaRequest(schema)))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await secretariat.PostAsync(new Uri($"/api/v1/report-types/{type.Id}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);
        return type;
    }

    private static async Task<Guid> OpenWindowAsync(HttpClient secretariat, Guid reportTypeId, string period)
    {
        var created = await (await secretariat.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", new CreateSubmissionWindowRequest(
                period, Now.AddDays(-1), 3, Now.AddDays(20), [new WindowDueDate(UnitLevelCode.Dila, Now.AddDays(10))])))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();
        return created!.Window.Id;
    }

    private static async Task<HttpResponseMessage> SaveAndSubmitAsync(HttpClient head, Guid windowId, Guid unitId)
    {
        var url = $"/api/v1/reports/{windowId}?unit={unitId}";
        var current = await head.GetAsync(new Uri(url, UriKind.Relative));
        using var save = new HttpRequestMessage(HttpMethod.Put, new Uri(url, UriKind.Relative))
        {
            Content = JsonContent.Create(new SaveReportDraftRequest(new() { ["members"] = 40 })),
        };
        if (current.Headers.ETag is { } etag)
        {
            save.Headers.TryAddWithoutValidation("If-Match", etag.Tag);
        }

        Assert.True((await head.SendAsync(save)).IsSuccessStatusCode);
        return await head.PostAsJsonAsync($"/api/v1/reports/{windowId}/submit?unit={unitId}", new SubmitReportRequest(Confirmed: true));
    }

    // "RC"-prefixed unit codes keep this file's units apart in the shared test database.
    private static async Task<Guid> CreateDilaAsync(HttpClient secretariat, string suffix)
    {
        var zone = await (await secretariat.PostAsJsonAsync("/api/v1/units", new CreateUnitRequest(
                $"RCZ{suffix}", $"Codes Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await secretariat.PostAsJsonAsync("/api/v1/units", new CreateUnitRequest(
                $"RCD{suffix}", $"Codes Dila {suffix}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        return dila!.Id;
    }

    private static string UniqueCode() => "Z" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant().Replace('0', 'A');

    [Fact]
    public async Task ACodeIsAssignedWhenLeftBlankAndAChosenOneMustBeValidAndFree()
    {
        var (secretariat, _) = await SignInAsync(RoleName.Secretariat);

        var first = await CreateTypeAsync(secretariat, Monthly("Codes auto one"));
        var second = await CreateTypeAsync(secretariat, Monthly("Codes auto two"));
        Assert.StartsWith("GMR", first.Code, StringComparison.Ordinal);
        Assert.StartsWith("GMR", second.Code, StringComparison.Ordinal);
        Assert.NotEqual(first.Code, second.Code);

        var code = UniqueCode();
        var chosen = await CreateTypeAsync(secretariat, Monthly("Codes chosen", code.ToLowerInvariant()));
        Assert.Equal(code, chosen.Code);

        var taken = await secretariat.PostAsJsonAsync("/api/v1/report-types", Monthly("Codes taken", code));
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Contains("REPORT_TYPE_CODE_TAKEN", await taken.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var invalid = await secretariat.PostAsJsonAsync("/api/v1/report-types", Monthly("Codes invalid", "G-1"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task TwoMonthlyTypesForOneUnitAndMonthBothSubmitWithTheirOwnReferencesAndTheCodeThenLocks()
    {
        var (secretariat, _) = await SignInAsync(RoleName.Secretariat);
        var dilaId = await CreateDilaAsync(secretariat, "01");
        var (head, headId) = await SignInAsync(null);
        await factory.CreateRoleAssignmentAsync(headId, dilaId, RoleName.UnitHead);

        var generalCode = UniqueCode();
        var dilaCode = UniqueCode();
        var general = await CreateTypeAsync(secretariat, Monthly("Codes general monthly", generalCode));
        var dilaMonthly = await CreateTypeAsync(secretariat, Monthly("Codes dila monthly", dilaCode));
        var generalWindow = await OpenWindowAsync(secretariat, general.Id, "September 2026");
        var dilaWindow = await OpenWindowAsync(secretariat, dilaMonthly.Id, "September 2026");

        var first = await SaveAndSubmitAsync(head, generalWindow, dilaId);
        var second = await SaveAndSubmitAsync(head, dilaWindow, dilaId);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal($"{generalCode}-2026-09-RCD01", (await first.Content.ReadFromJsonAsync<SubmitReportResult>())!.ReferenceNumber);
        Assert.Equal($"{dilaCode}-2026-09-RCD01", (await second.Content.ReadFromJsonAsync<SubmitReportResult>())!.ReferenceNumber);

        // A re-run of the same period in a second window gets a suffix, never an error.
        var rerunWindow = await OpenWindowAsync(secretariat, general.Id, "September 2026");
        var rerun = await SaveAndSubmitAsync(head, rerunWindow, dilaId);
        Assert.Equal(HttpStatusCode.OK, rerun.StatusCode);
        Assert.Equal($"{generalCode}-2026-09-RCD01-2", (await rerun.Content.ReadFromJsonAsync<SubmitReportResult>())!.ReferenceNumber);

        // References have been issued, so the code is now fixed; other edits still go through.
        var update = new UpdateReportTypeRequest(
            "Codes general monthly (renamed)", null, ReportTypeCategory.GeneralMonthly, ReportTypeFrequency.Monthly,
            [UnitLevelCode.Dila], UnitLevelCode.National, 1, false, false, Code: UniqueCode());
        var locked = await secretariat.PutAsJsonAsync($"/api/v1/report-types/{general.Id}", update);
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Contains("REPORT_TYPE_CODE_LOCKED", await locked.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var renamed = await secretariat.PutAsJsonAsync($"/api/v1/report-types/{general.Id}", update with { Code = null });
        using var body = JsonDocument.Parse(await renamed.Content.ReadAsStringAsync());
        Assert.Equal(generalCode, body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ACodeCanStillChangeBeforeAnyReportIsSubmitted()
    {
        var (secretariat, _) = await SignInAsync(RoleName.Secretariat);
        var type = await CreateTypeAsync(secretariat, Monthly("Codes changeable"));
        var newCode = UniqueCode();

        var response = await secretariat.PutAsJsonAsync($"/api/v1/report-types/{type.Id}", new UpdateReportTypeRequest(
            type.Name, null, ReportTypeCategory.GeneralMonthly, ReportTypeFrequency.Monthly,
            [UnitLevelCode.Dila], UnitLevelCode.National, 1, false, false, Code: newCode));

        Assert.Equal(newCode, (await response.Content.ReadFromJsonAsync<ReportTypeSummary>())!.Code);
    }
}
