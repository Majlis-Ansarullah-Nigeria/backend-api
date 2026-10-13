using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Notifications;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Review.Inbox;
using HRS.Application.Features.Review.Workload;
using HRS.Application.Features.Submissions.Comments;
using HRS.Application.Features.Submissions.ReportInstances;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

/// <summary>FR-REV-003 (field-anchored comment threads) and FR-REV-004 (assignment to staff +
/// workload view), M7.3. "CM"-prefixed unit codes — see InboxEndpointsTests's own note on why
/// every file in this collection needs its own prefix.</summary>
[Collection(ApiTestCollection.Name)]
public class ReportCommentsAndAssignmentTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CreateReportTypeRequest MonthlyReport(string name) => new(
        name, "Monthly activity figures", ReportTypeCategory.GeneralMonthly, ReportTypeFrequency.Monthly,
        [UnitLevelCode.Muqam], UnitLevelCode.National, 1, AllowAttachments: true, AllowNilReport: false);

    private async Task<(HttpClient Client, Guid UserId, string Email)> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Comment1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, userId, email);
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedUserAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Comment2";
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
            "welfare_case", TemplateFieldType.ShortText, "Welfare cases", null, false, null, null, null, null, null, false,
            null, null, null, AggregationRule.None, null, null, null);
        var schema = new TemplateSchema([new TemplateSection("section_1", "Welfare", [welfareCase])]);

        var draft = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(schema)))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);
    }

    private static async Task<Guid> CreateMuqamUnitAsync(HttpClient client, string suffix)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"CMZ{suffix}", $"Comment Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"CMD{suffix}", $"Comment Dila {suffix}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var muqam = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"CMM{suffix}", $"Comment Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dila!.Id, null, null, null, null, null)))
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

    private async Task<(Guid ReportId, HttpClient HeadClient, string HeadEmail)> SubmitMuqamReportAsHeadAsync(Guid windowId, Guid muqamId)
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Comment3";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        var saved = await (await SaveAsync(client, windowId, muqamId, new Dictionary<string, object?> { ["welfare_case"] = "x" }))
            .Content.ReadFromJsonAsync<SaveReportDraftResult>();
        await SubmitAsync(client, saved!.Id, muqamId);

        return (saved.Id, client, email);
    }

    private static Task<HttpResponseMessage> GetCommentsAsync(HttpClient client, Guid reportId) =>
        client.GetAsync(new Uri($"/api/v1/reports/{reportId}/comments", UriKind.Relative));

    private static Task<HttpResponseMessage> PostCommentAsync(HttpClient client, Guid reportId, string? fieldKey, Guid? parentId, string body) =>
        client.PostAsJsonAsync($"/api/v1/reports/{reportId}/comments", new AddCommentRequest(fieldKey, parentId, body));

    private static Task<HttpResponseMessage> AssignAsync(HttpClient client, Guid reportId, Guid? assignedToUserId) =>
        client.PutAsJsonAsync($"/api/v1/secretariat/inbox/{reportId}/assign", new AssignInboxReportRequest(assignedToUserId));

    private static async Task<InboxReportDetail?> GetInboxDetailAsync(HttpClient client, Guid reportId) =>
        await (await client.GetAsync(new Uri($"/api/v1/secretariat/inbox/{reportId}", UriKind.Relative))).Content.ReadFromJsonAsync<InboxReportDetail>();

    /// <summary>M8.1: a comment now queues an OutboxMessage instead of sending email directly —
    /// this runs the same dispatcher the Worker's recurring job runs, against the test database,
    /// so these tests can still assert against factory.EmailSender. Loops until a run sends
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
    public async Task SecretariatCanStartAThreadAndTheHeadCanReply()
    {
        var (secretariat, _, _) = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Comment Thread Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "01");
        var (reportId, headClient, headEmail) = await SubmitMuqamReportAsHeadAsync(windowId, muqamId);

        var rootResponse = await PostCommentAsync(secretariat, reportId, "welfare_case", null, "Can you clarify this figure?");
        Assert.Equal(HttpStatusCode.OK, rootResponse.StatusCode);
        var root = await rootResponse.Content.ReadFromJsonAsync<CommentView>();

        await RunOutboxDispatcherAsync();
        var sentToHead = factory.EmailSender.LastSentTo(headEmail);
        Assert.NotNull(sentToHead);
        Assert.Contains("Can you clarify this figure?", sentToHead!.TextBody);

        var replyResponse = await PostCommentAsync(headClient, reportId, null, root!.Id, "It's a typo, fixing now.");
        Assert.Equal(HttpStatusCode.OK, replyResponse.StatusCode);

        var threadsAsSecretariat = await (await GetCommentsAsync(secretariat, reportId)).Content.ReadFromJsonAsync<List<CommentThread>>();
        Assert.Single(threadsAsSecretariat!);
        var thread = threadsAsSecretariat![0];
        Assert.Equal("welfare_case", thread.FieldKey);
        Assert.Equal("Welfare cases", thread.FieldLabel);
        Assert.Equal("Can you clarify this figure?", thread.Root.Body);
        Assert.Single(thread.Replies);
        Assert.Equal("It's a typo, fixing now.", thread.Replies[0].Body);

        var threadsAsHead = await (await GetCommentsAsync(headClient, reportId)).Content.ReadFromJsonAsync<List<CommentThread>>();
        Assert.Single(threadsAsHead!);
    }

    [Fact]
    public async Task AHeadsReplyNotifiesTheAssignedStaffMemberOnlyWhenOneIsSet()
    {
        var (secretariat, _, _) = await CreateSecretariatClientAsync();
        var (_, staffUserId, staffEmail) = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Comment Assignment Notify Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "02");
        var (reportId, headClient, _) = await SubmitMuqamReportAsHeadAsync(windowId, muqamId);

        var root = await (await PostCommentAsync(secretariat, reportId, "welfare_case", null, "Please double check."))
            .Content.ReadFromJsonAsync<CommentView>();

        // Unassigned: the head's reply notifies nobody.
        await PostCommentAsync(headClient, reportId, null, root!.Id, "Looking into it.");
        await RunOutboxDispatcherAsync();
        Assert.Null(factory.EmailSender.LastSentTo(staffEmail));

        // Now assign, and reply again: the assignee gets the email.
        await AssignAsync(secretariat, reportId, staffUserId);
        await PostCommentAsync(headClient, reportId, null, root.Id, "Confirmed correct, no change needed.");

        await RunOutboxDispatcherAsync();
        var sentToStaff = factory.EmailSender.LastSentTo(staffEmail);
        Assert.NotNull(sentToStaff);
        Assert.Contains("Confirmed correct, no change needed.", sentToStaff!.TextBody);
    }

    [Fact]
    public async Task ReplyToAReplyIsRejected()
    {
        var (secretariat, _, _) = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Comment Nesting Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "03");
        var (reportId, headClient, _) = await SubmitMuqamReportAsHeadAsync(windowId, muqamId);

        var root = await (await PostCommentAsync(secretariat, reportId, "welfare_case", null, "Root comment"))
            .Content.ReadFromJsonAsync<CommentView>();
        var reply = await (await PostCommentAsync(headClient, reportId, null, root!.Id, "A reply"))
            .Content.ReadFromJsonAsync<CommentView>();

        var replyToReply = await PostCommentAsync(secretariat, reportId, null, reply!.Id, "Replying to a reply");

        Assert.Equal(HttpStatusCode.BadRequest, replyToReply.StatusCode);
    }

    [Fact]
    public async Task CommentValidationRejectsAnEmptyBodyAndAnUnknownField()
    {
        var (secretariat, _, _) = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Comment Validation Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "04");
        var (reportId, _, _) = await SubmitMuqamReportAsHeadAsync(windowId, muqamId);

        Assert.Equal(HttpStatusCode.BadRequest, (await PostCommentAsync(secretariat, reportId, "welfare_case", null, "   ")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostCommentAsync(secretariat, reportId, "not_a_real_field", null, "hello")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostCommentAsync(secretariat, reportId, null, null, "no field, no parent")).StatusCode);
    }

    [Fact]
    public async Task CommentsAreForbiddenForObserversAndUnrelatedUsers()
    {
        var (secretariat, _, _) = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Comment Access Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "05");
        var (reportId, _, _) = await SubmitMuqamReportAsHeadAsync(windowId, muqamId);

        var (unrelatedClient, _) = await CreateAuthenticatedUserAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await GetCommentsAsync(unrelatedClient, reportId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostCommentAsync(unrelatedClient, reportId, "welfare_case", null, "hi")).StatusCode);
    }

    [Fact]
    public async Task AssignRequiresAnActiveSecretariatUser()
    {
        var (secretariat, _, _) = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Assign Validation Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "06");
        var (reportId, _, _) = await SubmitMuqamReportAsHeadAsync(windowId, muqamId);
        var (_, notStaffUserId) = await CreateAuthenticatedUserAsync();

        var response = await AssignAsync(secretariat, reportId, notStaffUserId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AssignSetsAndClearsTheAssignee()
    {
        var (secretariat, _, _) = await CreateSecretariatClientAsync();
        var (_, staffUserId, _) = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Assign Set Clear Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "07");
        var (reportId, _, _) = await SubmitMuqamReportAsHeadAsync(windowId, muqamId);

        Assert.Equal(HttpStatusCode.NoContent, (await AssignAsync(secretariat, reportId, staffUserId)).StatusCode);
        var assigned = await GetInboxDetailAsync(secretariat, reportId);
        Assert.Equal(staffUserId, assigned!.AssignedToUserId);

        Assert.Equal(HttpStatusCode.NoContent, (await AssignAsync(secretariat, reportId, null)).StatusCode);
        var cleared = await GetInboxDetailAsync(secretariat, reportId);
        Assert.Null(cleared!.AssignedToUserId);
    }

    [Fact]
    public async Task AssignIsForbiddenForNonSecretariat()
    {
        var (secretariat, _, _) = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Assign Forbidden Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "08");
        var (reportId, headClient, _) = await SubmitMuqamReportAsHeadAsync(windowId, muqamId);

        var response = await AssignAsync(headClient, reportId, null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InboxFiltersByAssignedToUserId()
    {
        var (secretariat, _, _) = await CreateSecretariatClientAsync();
        var (_, staffUserId, _) = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Assigned To Me Filter Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);
        var assignedMuqamId = await CreateMuqamUnitAsync(secretariat, "09a");
        var unassignedMuqamId = await CreateMuqamUnitAsync(secretariat, "09b");
        var (assignedReportId, _, _) = await SubmitMuqamReportAsHeadAsync(windowId, assignedMuqamId);
        await SubmitMuqamReportAsHeadAsync(windowId, unassignedMuqamId);
        await AssignAsync(secretariat, assignedReportId, staffUserId);

        var filtered = await (await secretariat.GetAsync(
                new Uri($"/api/v1/secretariat/inbox?reportTypeId={reportTypeId}&assignedToUserId={staffUserId}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<InboxItem>>();

        Assert.Single(filtered!);
        Assert.Equal(assignedReportId, filtered![0].ReportId);
        Assert.Equal("Test User", filtered[0].AssignedToName);
    }

    [Fact]
    public async Task WorkloadCountsInPlayAssignedReportsPerStaffMemberAndTracksUnassigned()
    {
        var (secretariat, _, _) = await CreateSecretariatClientAsync();
        var (_, staffAId, _) = await CreateSecretariatClientAsync();
        var (_, staffBId, _) = await CreateSecretariatClientAsync();
        var (_, staffCId, _) = await CreateSecretariatClientAsync(); // never assigned anything

        var reportTypeId = await CreateReportTypeAsync(secretariat, "Workload Report");
        await PublishTemplateAsync(secretariat, reportTypeId);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);

        var muqamA = await CreateMuqamUnitAsync(secretariat, "10a");
        var muqamB = await CreateMuqamUnitAsync(secretariat, "10b");
        var muqamC = await CreateMuqamUnitAsync(secretariat, "10c");
        var muqamD = await CreateMuqamUnitAsync(secretariat, "10d");

        var (reportA, _, _) = await SubmitMuqamReportAsHeadAsync(windowId, muqamA); // -> staff A, stays Submitted
        var (reportB, _, _) = await SubmitMuqamReportAsHeadAsync(windowId, muqamB); // -> staff A, gets Acknowledged (shouldn't count)
        var (reportC, _, _) = await SubmitMuqamReportAsHeadAsync(windowId, muqamC); // -> staff B, stays Submitted
        await SubmitMuqamReportAsHeadAsync(windowId, muqamD); // unassigned

        await AssignAsync(secretariat, reportA, staffAId);
        await AssignAsync(secretariat, reportB, staffAId);
        await AssignAsync(secretariat, reportC, staffBId);
        await secretariat.PostAsync(new Uri($"/api/v1/secretariat/inbox/{reportB}/acknowledge", UriKind.Relative), null);

        var workload = await (await secretariat.GetAsync(new Uri("/api/v1/secretariat/workload", UriKind.Relative)))
            .Content.ReadFromJsonAsync<WorkloadView>();

        var staffA = workload!.Staff.Single(s => s.UserId == staffAId);
        var staffB = workload.Staff.Single(s => s.UserId == staffBId);
        var staffC = workload.Staff.Single(s => s.UserId == staffCId);

        Assert.Equal(1, staffA.AssignedCount); // only reportA counts; reportB is Acknowledged
        Assert.Equal(1, staffB.AssignedCount);
        Assert.Equal(0, staffC.AssignedCount);
        Assert.True(workload.UnassignedCount >= 1); // at least muqamD's report
    }

    [Fact]
    public async Task WorkloadIsForbiddenForNonSecretariat()
    {
        var (headClient, _) = await CreateAuthenticatedUserAsync();

        var response = await headClient.GetAsync(new Uri("/api/v1/secretariat/workload", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
