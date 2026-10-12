using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Review.Inbox;
using HRS.Application.Features.Review.SavedViews;
using HRS.Application.Features.Submissions.ReportInstances;
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
public class InboxEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CreateReportTypeRequest MonthlyReport(string name) => new(
        name, "Monthly activity figures", ReportTypeCategory.GeneralMonthly, ReportTypeFrequency.Monthly,
        [UnitLevelCode.Muqam], UnitLevelCode.National, 1, AllowAttachments: true, AllowNilReport: false);

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Inbox1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedUserAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Inbox2";
        var userId = await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, userId);
    }

    private static async Task<Guid> CreateReportTypeAsync(HttpClient client, string name)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MonthlyReport(name)))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        return created!.Id;
    }

    private static async Task PublishTemplateAsync(HttpClient client, Guid reportTypeId)
    {
        var welfareCase = new TemplateField(
            "welfare_case", TemplateFieldType.ShortText, "Confidential details", null, false, null, null, null, null, null, true,
            null, null, null, AggregationRule.None, null, null, null);
        var schema = new TemplateSchema([new TemplateSection("section_1", "Welfare", [welfareCase])]);

        var draft = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(schema)))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);
    }

    // "IB"-prefixed codes — every test in HRS.Api.IntegrationTests shares one Testcontainers
    // Postgres, so unit codes must be namespaced per file to avoid (ParentId, Code) collisions.
    private static async Task<Guid> CreateMuqamUnitAsync(HttpClient client, string suffix)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"IBZ{suffix}", $"Inbox Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"IBD{suffix}", $"Inbox Dila {suffix}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var muqam = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"IBM{suffix}", $"Inbox Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dila!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();

        return muqam!.Id;
    }

    private static async Task<Guid> CreateWindowAsync(HttpClient client, Guid reportTypeId, string periodLabel = "Sep 2026")
    {
        var request = new CreateSubmissionWindowRequest(
            periodLabel, Now.AddDays(-10), 3, Now.AddDays(20), [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))]);
        var created = await (await client.PostAsJsonAsync($"/api/v1/report-types/{reportTypeId}/windows", request))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>();

        return created!.Window.Id;
    }

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, Guid reportId, Guid unitId, Dictionary<string, object?> answers) =>
        client.PutAsJsonAsync($"/api/v1/reports/{reportId}?unit={unitId}", new SaveReportDraftRequest(answers));

    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid reportId, Guid unitId) =>
        client.PostAsJsonAsync($"/api/v1/reports/{reportId}/submit?unit={unitId}", new SubmitReportRequest(Confirmed: true));

    private async Task<Guid> SubmitMuqamReportAsync(Guid windowId, Guid muqamId, Dictionary<string, object?> answers)
    {
        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);
        var saved = await (await SaveAsync(client, windowId, muqamId, answers)).Content.ReadFromJsonAsync<SaveReportDraftResult>();
        await SubmitAsync(client, saved!.Id, muqamId);
        return saved.Id;
    }

    /// <summary>Same as SubmitMuqamReportAsync, but also hands back the head's own client and
    /// email — needed by the Acknowledge/Return tests to assert the notification landed on the
    /// current head and that the head's own editing view picks up the change.</summary>
    private async Task<(Guid ReportId, HttpClient HeadClient, string HeadEmail)> SubmitMuqamReportAsHeadAsync(
        Guid windowId, Guid muqamId, Dictionary<string, object?> answers)
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Inbox3";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        var saved = await (await SaveAsync(client, windowId, muqamId, answers)).Content.ReadFromJsonAsync<SaveReportDraftResult>();
        await SubmitAsync(client, saved!.Id, muqamId);

        return (saved.Id, client, email);
    }

    private static Task<HttpResponseMessage> GetInboxAsync(HttpClient client, string query = "") =>
        client.GetAsync(new Uri($"/api/v1/secretariat/inbox{query}", UriKind.Relative));

    private static async Task<InboxReportDetail?> GetInboxDetailAsync(HttpClient client, Guid reportId) =>
        await (await client.GetAsync(new Uri($"/api/v1/secretariat/inbox/{reportId}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<InboxReportDetail>();

    private static Task<HttpResponseMessage> OpenAsync(HttpClient client, Guid reportId) =>
        client.PostAsync(new Uri($"/api/v1/secretariat/inbox/{reportId}/open", UriKind.Relative), null);

    private static Task<HttpResponseMessage> AcknowledgeAsync(HttpClient client, Guid reportId) =>
        client.PostAsync(new Uri($"/api/v1/secretariat/inbox/{reportId}/acknowledge", UriKind.Relative), null);

    private static Task<HttpResponseMessage> ReturnAsync(HttpClient client, Guid reportId, string comment) =>
        client.PostAsJsonAsync($"/api/v1/secretariat/inbox/{reportId}/return", new ReturnInboxReportRequest(comment));

    private static Task<HttpResponseMessage> SetFlaggedAsync(HttpClient client, Guid reportId, bool isFlagged) =>
        client.PutAsJsonAsync($"/api/v1/secretariat/inbox/{reportId}/flag", new SetInboxReportFlaggedRequest(isFlagged));

    private static async Task<ReportInstanceForEditing?> GetReportForEditingAsync(HttpClient client, Guid reportId, Guid unitId) =>
        await (await client.GetAsync(new Uri($"/api/v1/reports/{reportId}?unit={unitId}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<ReportInstanceForEditing>();

    [Fact]
    public async Task InboxListsOnlySubmittedOrLaterReports()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Inbox Status Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "01");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);

        // A draft that's never submitted.
        var (draftClient, draftUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(draftUserId, muqamId, RoleName.UnitHead);
        await SaveAsync(draftClient, windowId, muqamId, new Dictionary<string, object?> { ["welfare_case"] = "draft only" });

        var reportId = await SubmitMuqamReportAsync(windowId, await CreateMuqamUnitAsync(secretariat, "01b"), new Dictionary<string, object?> { ["welfare_case"] = "x" });

        var items = await (await GetInboxAsync(secretariat, $"?reportTypeId={reportTypeId}"))
            .Content.ReadFromJsonAsync<List<InboxItem>>();

        Assert.Single(items!);
        Assert.Equal(reportId, items![0].ReportId);
        Assert.Equal(ReportInstanceStatus.Submitted, items[0].Status);
    }

    [Fact]
    public async Task InboxFiltersByZone()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Inbox Zone Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);

        var muqamA = await CreateMuqamUnitAsync(secretariat, "02a");
        var muqamB = await CreateMuqamUnitAsync(secretariat, "02b");
        await SubmitMuqamReportAsync(windowId, muqamA, new Dictionary<string, object?> { ["welfare_case"] = "a" });
        await SubmitMuqamReportAsync(windowId, muqamB, new Dictionary<string, object?> { ["welfare_case"] = "b" });

        var allItems = await (await GetInboxAsync(secretariat, $"?reportTypeId={reportTypeId}")).Content.ReadFromJsonAsync<List<InboxItem>>();
        Assert.Equal(2, allItems!.Count);
        var zoneAId = allItems.Single(i => i.UnitId == muqamA).ZoneUnitId;

        var zoneFiltered = await (await GetInboxAsync(secretariat, $"?reportTypeId={reportTypeId}&zoneUnitId={zoneAId}"))
            .Content.ReadFromJsonAsync<List<InboxItem>>();

        Assert.Single(zoneFiltered!);
        Assert.Equal(muqamA, zoneFiltered![0].UnitId);
    }

    [Fact]
    public async Task InboxFiltersByWindowReportTypeStatusAndLate()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Inbox MultiFilter Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var augWindowId = await CreateWindowAsync(secretariat, reportTypeId, "Aug 2026");
        var sepWindowId = await CreateWindowAsync(secretariat, reportTypeId, "Sep 2026");

        var muqamAug = await CreateMuqamUnitAsync(secretariat, "03a");
        var muqamSep = await CreateMuqamUnitAsync(secretariat, "03b");
        await SubmitMuqamReportAsync(augWindowId, muqamAug, new Dictionary<string, object?> { ["welfare_case"] = "aug" });
        var sepReportId = await SubmitMuqamReportAsync(sepWindowId, muqamSep, new Dictionary<string, object?> { ["welfare_case"] = "sep" });

        var byWindow = await (await GetInboxAsync(secretariat, $"?windowId={sepWindowId}")).Content.ReadFromJsonAsync<List<InboxItem>>();
        Assert.Single(byWindow!);
        Assert.Equal(sepReportId, byWindow![0].ReportId);

        var byStatus = await (await GetInboxAsync(secretariat, $"?reportTypeId={reportTypeId}&status={(int)ReportInstanceStatus.Submitted}"))
            .Content.ReadFromJsonAsync<List<InboxItem>>();
        Assert.Equal(2, byStatus!.Count);

        var byLevel = await (await GetInboxAsync(secretariat, $"?reportTypeId={reportTypeId}&unitLevel={(int)UnitLevelCode.Muqam}"))
            .Content.ReadFromJsonAsync<List<InboxItem>>();
        Assert.Equal(2, byLevel!.Count);

        var byLate = await (await GetInboxAsync(secretariat, $"?reportTypeId={reportTypeId}&isLate=true")).Content.ReadFromJsonAsync<List<InboxItem>>();
        Assert.Empty(byLate!);
    }

    [Fact]
    public async Task InboxFiltersByFlagged()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Inbox Flagged Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "04");
        var reportId = await SubmitMuqamReportAsync(windowId, muqamId, new Dictionary<string, object?> { ["welfare_case"] = "x" });

        // No endpoint sets IsFlagged yet (that's M7.2) — set it directly to prove the filter works
        // against the column now that it exists.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var instance = await db.ReportInstances.FirstAsync(i => i.Id == reportId);
        instance.SetFlagged(true);
        await db.SaveChangesAsync();

        var flagged = await (await GetInboxAsync(secretariat, $"?reportTypeId={reportTypeId}&isFlagged=true")).Content.ReadFromJsonAsync<List<InboxItem>>();
        var unflagged = await (await GetInboxAsync(secretariat, $"?reportTypeId={reportTypeId}&isFlagged=false")).Content.ReadFromJsonAsync<List<InboxItem>>();

        Assert.Single(flagged!);
        Assert.True(flagged![0].IsFlagged);
        Assert.Empty(unflagged!);
    }

    [Fact]
    public async Task InboxIsForbiddenForNonSecretariatIncludingSysAdmin()
    {
        var (headClient, _) = await CreateAuthenticatedUserAsync();
        var headResponse = await GetInboxAsync(headClient);
        Assert.Equal(HttpStatusCode.Forbidden, headResponse.StatusCode);

        var (sysAdminClient, sysAdminUserId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(sysAdminUserId, OrganizationSeedIds.NationalUnitId, RoleName.SysAdmin);
        var sysAdminResponse = await GetInboxAsync(sysAdminClient);
        Assert.Equal(HttpStatusCode.Forbidden, sysAdminResponse.StatusCode);
    }

    [Fact]
    public async Task InboxReportDetailIsNotMaskedForConfidentialFields()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Inbox Detail Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "05");
        var reportId = await SubmitMuqamReportAsync(windowId, muqamId, new Dictionary<string, object?> { ["welfare_case"] = "Family X needs support" });

        var detail = await (await secretariat.GetAsync(new Uri($"/api/v1/secretariat/inbox/{reportId}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<InboxReportDetail>();

        var field = detail!.Receipt.Sections.SelectMany(s => s.Fields).Single(f => f.Label == "Confidential details");
        Assert.Equal("Family X needs support", field.Value);
        Assert.False(field.IsMasked);
    }

    [Fact]
    public async Task SavedViewsAreScopedToTheCreatingUser()
    {
        using var secretariatA = await CreateSecretariatClientAsync();
        using var secretariatB = await CreateSecretariatClientAsync();

        var createResponse = await secretariatA.PostAsJsonAsync(
            "/api/v1/secretariat/saved-views", new CreateSavedViewRequest("My overdue zone", new InboxFilter(null, null, null, null, null, true, null, null)));
        var created = await createResponse.Content.ReadFromJsonAsync<SavedViewSummary>();

        var aViews = await (await secretariatA.GetAsync(new Uri("/api/v1/secretariat/saved-views", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<SavedViewSummary>>();
        var bViews = await (await secretariatB.GetAsync(new Uri("/api/v1/secretariat/saved-views", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<SavedViewSummary>>();

        Assert.Contains(aViews!, v => v.Id == created!.Id);
        Assert.DoesNotContain(bViews!, v => v.Id == created!.Id);

        var deleteByOther = await secretariatB.DeleteAsync(new Uri($"/api/v1/secretariat/saved-views/{created!.Id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NotFound, deleteByOther.StatusCode);

        var deleteByOwner = await secretariatA.DeleteAsync(new Uri($"/api/v1/secretariat/saved-views/{created.Id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NoContent, deleteByOwner.StatusCode);
    }

    [Fact]
    public async Task OpenIsIdempotentAndMovesSubmittedToInReview()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Inbox Open Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "06");
        var reportId = await SubmitMuqamReportAsync(windowId, muqamId, new Dictionary<string, object?> { ["welfare_case"] = "x" });

        var first = await OpenAsync(secretariat, reportId);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(ReportInstanceStatus.InReview, (await GetInboxDetailAsync(secretariat, reportId))!.Status);

        // Opening an already-open report is a no-op, not a conflict (FR-REV-002's "mark as read"
        // is idempotent — every detail-view load fires it).
        var second = await OpenAsync(secretariat, reportId);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(ReportInstanceStatus.InReview, (await GetInboxDetailAsync(secretariat, reportId))!.Status);
    }

    [Fact]
    public async Task AcknowledgeAutoOpensAndNotifiesTheCurrentHead()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Inbox Acknowledge Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "07");
        var (reportId, _, headEmail) = await SubmitMuqamReportAsHeadAsync(windowId, muqamId, new Dictionary<string, object?> { ["welfare_case"] = "x" });

        // Never opened explicitly — Acknowledge must open it itself (Submitted -> InReview -> Acknowledged).
        var response = await AcknowledgeAsync(secretariat, reportId);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var detail = await GetInboxDetailAsync(secretariat, reportId);
        Assert.Equal(ReportInstanceStatus.Acknowledged, detail!.Status);

        var sent = factory.EmailSender.LastSentTo(headEmail);
        Assert.NotNull(sent);
        Assert.Contains("acknowledged", sent!.Subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcknowledgeFailsWhenTheReportIsAlreadyAcknowledged()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Inbox Double Acknowledge Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "08");
        var reportId = await SubmitMuqamReportAsync(windowId, muqamId, new Dictionary<string, object?> { ["welfare_case"] = "x" });

        Assert.Equal(HttpStatusCode.NoContent, (await AcknowledgeAsync(secretariat, reportId)).StatusCode);

        var second = await AcknowledgeAsync(secretariat, reportId);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task ReturnRequiresAComment()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Inbox Return Validation Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "09");
        var reportId = await SubmitMuqamReportAsync(windowId, muqamId, new Dictionary<string, object?> { ["welfare_case"] = "x" });

        var response = await ReturnAsync(secretariat, reportId, "   ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReturnTransitionsToReturnedNotifiesTheHeadAndTheCommentSurfacesOnTheHeadsEditingView()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Inbox Return Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "10");
        var (reportId, headClient, headEmail) = await SubmitMuqamReportAsHeadAsync(windowId, muqamId, new Dictionary<string, object?> { ["welfare_case"] = "x" });

        var response = await ReturnAsync(secretariat, reportId, "Please double-check the welfare figures.");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var detail = await GetInboxDetailAsync(secretariat, reportId);
        Assert.Equal(ReportInstanceStatus.Returned, detail!.Status);
        Assert.Equal("Please double-check the welfare figures.", detail.ReturnComment);

        var sent = factory.EmailSender.LastSentTo(headEmail);
        Assert.NotNull(sent);
        Assert.Contains("returned", sent!.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Please double-check the welfare figures.", sent.TextBody);

        var forEditing = await GetReportForEditingAsync(headClient, reportId, muqamId);
        Assert.Equal("Please double-check the welfare figures.", forEditing!.ReturnComment);
        Assert.True(forEditing.CanSave);
        Assert.NotNull(forEditing.RevisionDeadline);
    }

    [Fact]
    public async Task FlagCanBeSetAndClearedRegardlessOfStatus()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Inbox Flag Action Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "11");
        var reportId = await SubmitMuqamReportAsync(windowId, muqamId, new Dictionary<string, object?> { ["welfare_case"] = "x" });

        Assert.Equal(HttpStatusCode.NoContent, (await SetFlaggedAsync(secretariat, reportId, true)).StatusCode);
        Assert.True((await GetInboxDetailAsync(secretariat, reportId))!.IsFlagged);

        Assert.Equal(HttpStatusCode.NoContent, (await SetFlaggedAsync(secretariat, reportId, false)).StatusCode);
        Assert.False((await GetInboxDetailAsync(secretariat, reportId))!.IsFlagged);
    }

    [Fact]
    public async Task InboxActionsAreForbiddenForNonSecretariat()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Inbox Action Forbidden Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "12");
        var reportId = await SubmitMuqamReportAsync(windowId, muqamId, new Dictionary<string, object?> { ["welfare_case"] = "x" });

        var (headClient, _) = await CreateAuthenticatedUserAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await OpenAsync(headClient, reportId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AcknowledgeAsync(headClient, reportId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ReturnAsync(headClient, reportId, "comment")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SetFlaggedAsync(headClient, reportId, true)).StatusCode);
    }
}
