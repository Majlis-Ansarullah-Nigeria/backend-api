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
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class ReportSubmissionEndpointsTests(ApiFactory factory)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static CreateReportTypeRequest MonthlyReport(string name) => new(
        name, "Monthly activity figures", ReportTypeCategory.GeneralMonthly, ReportTypeFrequency.Monthly,
        [UnitLevelCode.Muqam], UnitLevelCode.National, 1, AllowAttachments: true);

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Sub1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<(HttpClient Client, Guid UserId, string Email)> CreateAuthenticatedUserAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Sub2";
        var userId = await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, userId, email);
    }

    private static async Task<Guid> CreateReportTypeAsync(HttpClient client, string name)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MonthlyReport(name)))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        return created!.Id;
    }

    /// <summary>One required ShortText field and one (optional) Number field — enough to
    /// exercise the completeness gate and the review-warnings comparison.</summary>
    private static async Task PublishTemplateAsync(HttpClient client, Guid reportTypeId)
    {
        var notes = new TemplateField(
            "notes", TemplateFieldType.ShortText, "Notes", null, true, null, null, null, null, null, false,
            null, null, null, AggregationRule.None, null, null, null);
        var fundsCollected = new TemplateField(
            "funds_collected", TemplateFieldType.Number, "Funds collected", null, false, null, null, null, null, null, false,
            null, null, null, AggregationRule.None, null, null, null);
        var schema = new TemplateSchema([new TemplateSection("section_1", "Section 1", [notes, fundsCollected])]);

        var draft = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(schema)))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);
    }

    // "RS"-prefixed codes — every test in HRS.Api.IntegrationTests shares one Testcontainers
    // Postgres, so unit codes must be namespaced per file to avoid (ParentId, Code) collisions.
    private static async Task<Guid> CreateMuqamUnitAsync(HttpClient client, string suffix)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"RSZ{suffix}", $"Report Submission Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"RSD{suffix}", $"Report Submission Dila {suffix}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var muqam = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"RSM{suffix}", $"Report Submission Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dila!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();

        return muqam!.Id;
    }

    private static async Task<Guid> CreateWindowAsync(
        HttpClient client, Guid reportTypeId, DateTimeOffset opensAt, int graceDays, DateTimeOffset closesAt, DateTimeOffset dueAt)
    {
        var request = new CreateSubmissionWindowRequest(
            "Report Submission Test Window", opensAt, graceDays, closesAt, [new WindowDueDate(UnitLevelCode.Muqam, dueAt)]);
        var created = await (await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        return created!.Window.Id;
    }

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, Guid reportId, Guid unitId, Dictionary<string, object?> answers) =>
        client.PutAsJsonAsync($"/api/v1/reports/{reportId}?unit={unitId}", new SaveReportDraftRequest(answers));

    private static HttpRequestMessage SubmitRequest(Guid reportId, Guid unitId, bool confirmed, string? idempotencyKey = null)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/v1/reports/{reportId}/submit?unit={unitId}", UriKind.Relative))
        {
            Content = JsonContent.Create(new SubmitReportRequest(confirmed)),
        };

        if (idempotencyKey is not null)
        {
            message.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        return message;
    }

    /// <summary>Full setup: a secretariat-created report type with a published template (one
    /// required "notes" field, one optional numeric "funds_collected" field), a Muqam unit, an
    /// open window, and a UnitHead client with a saved, complete draft ready to submit.</summary>
    private async Task<(HttpClient Client, Guid ReportId, Guid UnitId, string Email)> SetUpReadyToSubmitAsync(string suffix)
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, $"Report Submission Report {suffix}");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, suffix);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId, email) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        await SaveAsync(client, windowId, muqamId, new Dictionary<string, object?> { ["notes"] = "All quiet this month." });

        return (client, windowId, muqamId, email);
    }

    [Fact]
    public async Task SubmitRequiresTheDeclarationToBeConfirmed()
    {
        var (client, reportId, unitId, _) = await SetUpReadyToSubmitAsync("01");

        using var request = SubmitRequest(reportId, unitId, confirmed: false);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitFailsWhenARequiredFieldIsStillEmpty()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Submission Incomplete Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "02");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);
        // Saved, but "notes" (required) was never filled in.
        await SaveAsync(client, windowId, muqamId, new Dictionary<string, object?> { ["funds_collected"] = 1000 });

        using var request = SubmitRequest(windowId, muqamId, confirmed: true);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitFailsWhenNothingHasEverBeenSaved()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Submission Nothing Saved Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "03");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        using var request = SubmitRequest(windowId, muqamId, confirmed: true);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SubmitSucceedsAssignsAReferenceNumberAndEmailsAPdfReceipt()
    {
        var (client, reportId, unitId, email) = await SetUpReadyToSubmitAsync("04");

        using var request = SubmitRequest(reportId, unitId, confirmed: true);
        var response = await client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<SubmitReportResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("GMR-", result!.ReferenceNumber, StringComparison.Ordinal);
        Assert.False(result.IsLate);

        // CapturingEmailSender.QueueAsync completes synchronously (no real background channel in
        // tests), so by the time the submit response has come back the email is already captured.
        var sentEmail = factory.EmailSender.LastSentTo(email);
        Assert.NotNull(sentEmail);
        Assert.Contains(result.ReferenceNumber, sentEmail!.Subject, StringComparison.Ordinal);
        Assert.NotNull(sentEmail.Attachment);
        Assert.Equal("application/pdf", sentEmail.Attachment!.ContentType);
        Assert.True(sentEmail.Attachment.Content.Length > 0);
    }

    [Fact]
    public async Task SubmitIsIdempotentOnARetriedKey()
    {
        var (client, reportId, unitId, _) = await SetUpReadyToSubmitAsync("05");
        var idempotencyKey = Guid.NewGuid().ToString();

        using var first = SubmitRequest(reportId, unitId, confirmed: true, idempotencyKey);
        var firstResult = await (await client.SendAsync(first)).Content.ReadFromJsonAsync<SubmitReportResult>();

        using var second = SubmitRequest(reportId, unitId, confirmed: true, idempotencyKey);
        var secondResponse = await client.SendAsync(second);
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<SubmitReportResult>();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(firstResult!.ReferenceNumber, secondResult!.ReferenceNumber);
        Assert.Equal(firstResult.SubmittedAt, secondResult.SubmittedAt);
    }

    [Fact]
    public async Task SubmitFailsAfterTheWindowCloses()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Submission Closed Window Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "06");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-30), 0, Now.AddDays(-1), Now.AddDays(-10));

        var (client, userId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        using var request = SubmitRequest(windowId, muqamId, confirmed: true);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ADraftOnlyDelegateCannotSubmit()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Submission Draft Only Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "07");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.Delegate, DelegateScope.DraftOnly);
        await SaveAsync(client, windowId, muqamId, new Dictionary<string, object?> { ["notes"] = "Filled by a delegate." });

        using var request = SubmitRequest(windowId, muqamId, confirmed: true);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ADraftAndSubmitDelegateCanSubmit()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Submission Draft And Submit Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "08");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.Delegate, DelegateScope.DraftAndSubmit);
        await SaveAsync(client, windowId, muqamId, new Dictionary<string, object?> { ["notes"] = "Filled by a delegate." });

        using var request = SubmitRequest(windowId, muqamId, confirmed: true);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReviewWarningsIsEmptyWithNoPriorSubmission()
    {
        var (client, reportId, unitId, _) = await SetUpReadyToSubmitAsync("09");

        var response = await client.GetAsync(new Uri($"/api/v1/reports/{reportId}/review-warnings?unit={unitId}", UriKind.Relative));
        var result = await response.Content.ReadFromJsonAsync<ReviewWarningsResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(result!.Warnings);
    }

    [Fact]
    public async Task ReviewWarningsFlagsALargeSwingFromThePriorSubmission()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Submission Warnings Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "10");

        var (client, userId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        // First period: submit with funds_collected = 1000. Both windows must still be open
        // (ReportEditability rejects a save/submit into an already-closed one) — the "earlier
        // period" comparison only needs this submission's SubmittedAt to land before the
        // second's, which sequential awaits already guarantee.
        var firstWindowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-5), 3, Now.AddDays(60), Now.AddDays(20));
        await SaveAsync(client, firstWindowId, muqamId, new Dictionary<string, object?> { ["notes"] = "First", ["funds_collected"] = 1000 });
        using var firstSubmit = SubmitRequest(firstWindowId, muqamId, confirmed: true);
        var firstSubmitResponse = await client.SendAsync(firstSubmit);
        Assert.Equal(HttpStatusCode.OK, firstSubmitResponse.StatusCode);

        // Second period: funds_collected = 100 — a 90% drop, well over the 50% threshold.
        var secondWindowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));
        await SaveAsync(client, secondWindowId, muqamId, new Dictionary<string, object?> { ["notes"] = "Second", ["funds_collected"] = 100 });

        var response = await client.GetAsync(new Uri($"/api/v1/reports/{secondWindowId}/review-warnings?unit={muqamId}", UriKind.Relative));
        var result = await response.Content.ReadFromJsonAsync<ReviewWarningsResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(result!.Warnings);
        Assert.Contains("Funds collected", result.Warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReceiptPdfDownloadsBeforeSubmitInDraftMode()
    {
        var (client, reportId, unitId, _) = await SetUpReadyToSubmitAsync("11");

        var response = await client.GetAsync(new Uri($"/api/v1/reports/{reportId}/receipt-pdf?unit={unitId}", UriKind.Relative));
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task ReceiptPdfDownloadsAfterSubmit()
    {
        var (client, reportId, unitId, _) = await SetUpReadyToSubmitAsync("12");
        using var submit = SubmitRequest(reportId, unitId, confirmed: true);
        await client.SendAsync(submit);

        var response = await client.GetAsync(new Uri($"/api/v1/reports/{reportId}/receipt-pdf?unit={unitId}", UriKind.Relative));
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(bytes.Length > 0);
    }
}
