using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Notifications;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Submissions.ReportInstances;
using HRS.Application.Features.Submissions.Review;
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

[Collection(ApiTestCollection.Name)]
public class ReportSubmissionEndpointsTests(ApiFactory factory)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static CreateReportTypeRequest MonthlyReport(string name, bool allowNilReport = false) => new(
        name, "Monthly activity figures", ReportTypeCategory.GeneralMonthly, ReportTypeFrequency.Monthly,
        [UnitLevelCode.Muqam], UnitLevelCode.National, 1, AllowAttachments: true, AllowNilReport: allowNilReport);

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

    private static async Task<Guid> CreateReportTypeAsync(HttpClient client, string name, bool allowNilReport = false, string? code = null)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MonthlyReport(name, allowNilReport) with { Code = code }))
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

    private static HttpRequestMessage SubmitRequest(Guid reportId, Guid unitId, bool confirmed, string? idempotencyKey = null, bool isNil = false)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, new Uri($"/api/v1/reports/{reportId}/submit?unit={unitId}", UriKind.Relative))
        {
            Content = JsonContent.Create(new SubmitReportRequest(confirmed, isNil)),
        };

        if (idempotencyKey is not null)
        {
            message.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        return message;
    }

    private static Task<HttpResponseMessage> WithdrawAsync(HttpClient client, Guid reportId, Guid unitId) =>
        client.PostAsync(new Uri($"/api/v1/reports/{reportId}/withdraw?unit={unitId}", UriKind.Relative), null);

    /// <summary>Full setup: a secretariat-created report type with a published template (one
    /// required "notes" field, one optional numeric "funds_collected" field), a Muqam unit, an
    /// open window, and a UnitHead client with a saved, complete draft ready to submit.</summary>
    private async Task<(HttpClient Client, Guid ReportId, Guid UnitId, string Email)> SetUpReadyToSubmitAsync(string suffix)
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, $"Report Submission Report {suffix}", code: $"RS{suffix}");
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
        // FR-SUB-010: {type code}-{period}-{unit code}; the type was created with code RS04.
        Assert.StartsWith("RS04-", result!.ReferenceNumber, StringComparison.Ordinal);
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

    [Fact]
    public async Task WithdrawMovesASubmittedUnreadReportBackToDraft()
    {
        var (client, reportId, unitId, _) = await SetUpReadyToSubmitAsync("13");
        using var submit = SubmitRequest(reportId, unitId, confirmed: true);
        await client.SendAsync(submit);

        var response = await WithdrawAsync(client, reportId, unitId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await client.GetAsync(new Uri($"/api/v1/reports/{reportId}?unit={unitId}", UriKind.Relative));
        var report = await getResponse.Content.ReadFromJsonAsync<ReportInstanceForEditing>();
        Assert.Equal(ReportInstanceStatus.Draft, report!.Status);
    }

    [Fact]
    public async Task WithdrawFailsWhenTheReportWasNeverSubmitted()
    {
        var (client, reportId, unitId, _) = await SetUpReadyToSubmitAsync("14");

        var response = await WithdrawAsync(client, reportId, unitId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task WithdrawFailsOnceTheSecretariatHasOpenedIt()
    {
        var (client, reportId, unitId, _) = await SetUpReadyToSubmitAsync("15");
        using var submit = SubmitRequest(reportId, unitId, confirmed: true);
        await client.SendAsync(submit);
        await OpenInstanceAsync(reportId, unitId);

        var response = await WithdrawAsync(client, reportId, unitId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task NilSubmitSucceedsFromAReportThatWasNeverSaved()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Submission Nil Report", allowNilReport: true);
        await PublishTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "16");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (client, userId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        // Nothing was ever saved — "notes" (required) was never filled in — yet the nil
        // submission still succeeds, bypassing the completeness gate entirely.
        using var request = SubmitRequest(windowId, muqamId, confirmed: true, isNil: true);
        var response = await client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<SubmitReportResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await client.GetAsync(new Uri($"/api/v1/reports/{windowId}?unit={muqamId}", UriKind.Relative));
        var report = await getResponse.Content.ReadFromJsonAsync<ReportInstanceForEditing>();
        Assert.True(report!.IsNil);
        Assert.Equal(result!.Id, report.Id);
    }

    [Fact]
    public async Task NilSubmitFailsWhenTheReportTypeDoesNotAllowIt()
    {
        var (client, reportId, unitId, _) = await SetUpReadyToSubmitAsync("17");

        using var request = SubmitRequest(reportId, unitId, confirmed: true, isNil: true);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PreviousAnswersIsNoContentWithNoPriorSubmission()
    {
        var (client, reportId, unitId, _) = await SetUpReadyToSubmitAsync("18");

        var response = await client.GetAsync(new Uri($"/api/v1/reports/{reportId}/previous-answers?unit={unitId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task PreviousAnswersReturnsTheMostRecentPriorSubmission()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Submission Previous Answers Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "19");

        var (client, userId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        var firstWindowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-5), 3, Now.AddDays(60), Now.AddDays(20));
        await SaveAsync(client, firstWindowId, muqamId, new Dictionary<string, object?> { ["notes"] = "From last period" });
        using var firstSubmit = SubmitRequest(firstWindowId, muqamId, confirmed: true);
        await client.SendAsync(firstSubmit);

        var secondWindowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var response = await client.GetAsync(new Uri($"/api/v1/reports/{secondWindowId}/previous-answers?unit={muqamId}", UriKind.Relative));
        var previous = await response.Content.ReadFromJsonAsync<PreviousReportAnswers>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("From last period", previous!.Answers["notes"]!.ToString());
    }

    private async Task OpenInstanceAsync(Guid windowOrInstanceId, Guid unitId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instance = await db.ReportInstances.FirstAsync(i => i.UnitId == unitId
            && (i.Id == windowOrInstanceId || i.WindowId == windowOrInstanceId));
        instance.Open();
        await db.SaveChangesAsync();
    }

    /// <summary>M8.1: runs the same dispatcher the Worker's recurring job runs, against the test
    /// database, so these tests can assert against factory.EmailSender. Loops until a run sends
    /// nothing more: every test in HRS.Api.IntegrationTests shares one never-reset Postgres, so
    /// by the time later test classes run there can be a real backlog of other tests' pending
    /// messages ahead of this one's in the dispatcher's oldest-first batch.</summary>
    private async Task RunOutboxDispatcherAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        int sent;
        var iterations = 0;
        do
        {
            sent = await OutboxDispatcher.RunAsync(db, identityService, factory.EmailSender, factory.PushSender, factory.Broadcaster, clock, CancellationToken.None);
            iterations++;
        }
        while (sent > 0 && iterations < 200);
    }

    [Fact]
    public async Task SubmittingNotifiesTheParentObserversCurrentHead()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Child Submitted Notify Report");
        await PublishTemplateAsync(secretariat, reportTypeId);

        var zone = await (await secretariat.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest("RSZcs", "CS Notify Zone", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await secretariat.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest("RSDcs", "CS Notify Dila", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var muqam = await (await secretariat.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest("RSMcs", "CS Notify Muqam", OrganizationSeedIds.MuqamLevelId, dila!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();

        var (_, dilaHeadUserId, dilaHeadEmail) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(dilaHeadUserId, dila.Id, RoleName.UnitHead);

        var windowId = await CreateWindowAsync(secretariat, reportTypeId, Now.AddDays(-1), 3, Now.AddDays(30), Now.AddDays(10));

        var (muqamHeadClient, muqamHeadUserId, _) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(muqamHeadUserId, muqam!.Id, RoleName.UnitHead);
        var saved = await (await SaveAsync(muqamHeadClient, windowId, muqam.Id, new Dictionary<string, object?> { ["notes"] = "x" }))
            .Content.ReadFromJsonAsync<SaveReportDraftResult>();
        await muqamHeadClient.SendAsync(SubmitRequest(saved!.Id, muqam.Id, true));

        await RunOutboxDispatcherAsync();

        var sent = factory.EmailSender.LastSentTo(dilaHeadEmail);
        Assert.NotNull(sent);
        Assert.Contains("CS Notify Muqam", sent!.Subject);
    }
}
