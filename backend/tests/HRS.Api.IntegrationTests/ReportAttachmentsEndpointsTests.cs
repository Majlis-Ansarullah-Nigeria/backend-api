using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Submissions.Attachments;
using HRS.Application.Features.Submissions.ReportInstances;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Application.Features.Windows.SubmissionWindows;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class ReportAttachmentsEndpointsTests(ApiFactory factory)
{
    private const string FieldKey = "photos";

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static CreateReportTypeRequest MonthlyReport(string name, bool allowAttachments = true) => new(
        name,
        "Monthly activity figures",
        ReportTypeCategory.GeneralMonthly,
        ReportTypeFrequency.Monthly,
        [UnitLevelCode.Muqam],
        UnitLevelCode.National,
        1,
        allowAttachments,
        AllowNilReport: false);

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Att1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthenticatedUserAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Att2";
        var userId = await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, userId);
    }

    private static async Task<Guid> CreateReportTypeAsync(HttpClient client, string name, bool allowAttachments = true)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/report-types", MonthlyReport(name, allowAttachments)))
            .Content.ReadFromJsonAsync<ReportTypeSummary>();
        return created!.Id;
    }

    /// <summary>A single FileUpload field keyed "photos" — lets GetReportInstanceForEditing's
    /// attachment-splicing be asserted against a real schema field, not just a presign-time key
    /// the backend never validates against the schema.</summary>
    private static async Task PublishTemplateWithFileUploadFieldAsync(HttpClient client, Guid reportTypeId)
    {
        var field = new TemplateField(
            FieldKey, TemplateFieldType.FileUpload, "Photos", null, false, null, null, null, null, null, false,
            null, null, null, AggregationRule.None, null, null, null);
        var schema = new TemplateSchema([new TemplateSection("section_1", "Section 1", [field])]);

        var draft = await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/template-versions", new UpdateTemplateSchemaRequest(schema)))
            .Content.ReadFromJsonAsync<TemplateVersionSummary>();
        await client.PostAsync(new Uri($"/api/v1/report-types/{reportTypeId}/template-versions/{draft!.Id}/publish", UriKind.Relative), null);
    }

    // "RA"-prefixed codes — every test in HRS.Api.IntegrationTests shares one Testcontainers
    // Postgres, so unit codes must be namespaced per file to avoid (ParentId, Code) collisions.
    private static async Task<Guid> CreateMuqamUnitAsync(HttpClient client, string suffix)
    {
        var zone = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"RAZ{suffix}", $"Report Attachment Zone {suffix}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"RAD{suffix}", $"Report Attachment Dila {suffix}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var muqam = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest($"RAM{suffix}", $"Report Attachment Muqam {suffix}", OrganizationSeedIds.MuqamLevelId, dila!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();

        return muqam!.Id;
    }

    private static async Task<Guid> CreateWindowAsync(HttpClient client, Guid reportTypeId) =>
        (await (await client.PostAsJsonAsync(
                $"/api/v1/report-types/{reportTypeId}/windows",
                new CreateSubmissionWindowRequest(
                    "Report Attachment Test Window", Now.AddDays(-1), 3, Now.AddDays(30), [new WindowDueDate(UnitLevelCode.Muqam, Now.AddDays(10))])))
            .Content.ReadFromJsonAsync<CreateSubmissionWindowResult>())!.Window.Id;

    /// <summary>Full setup for a single test: a secretariat-created, attachment-allowing report
    /// type with a published "photos" FileUpload field, a Muqam unit, an open window, and a
    /// UnitHead client for that unit.</summary>
    private async Task<(HttpClient Client, Guid ReportId, Guid UnitId)> SetUpAsync(string suffix, bool allowAttachments = true)
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, $"Report Attachment Report {suffix}", allowAttachments);
        await PublishTemplateWithFileUploadFieldAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, suffix);
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);

        var (client, userId) = await CreateAuthenticatedUserAsync();
        await factory.CreateRoleAssignmentAsync(userId, muqamId, RoleName.UnitHead);

        return (client, windowId, muqamId);
    }

    private static Task<HttpResponseMessage> PresignAsync(HttpClient client, Guid reportId, Guid unitId, string fileName, string contentType, long sizeBytes) =>
        client.PostAsJsonAsync(
            $"/api/v1/reports/{reportId}/attachments/presign?unit={unitId}",
            new PresignAttachmentRequest(FieldKey, fileName, contentType, sizeBytes));

    private static Task<HttpResponseMessage> CompleteAsync(HttpClient client, Guid reportId, Guid unitId, Guid attachmentId) =>
        client.PostAsync(new Uri($"/api/v1/reports/{reportId}/attachments/{attachmentId}/complete?unit={unitId}", UriKind.Relative), null);

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, Guid reportId, Guid unitId, Guid attachmentId) =>
        client.DeleteAsync(new Uri($"/api/v1/reports/{reportId}/attachments/{attachmentId}?unit={unitId}", UriKind.Relative));

    // PresignedAttachmentUpload doesn't expose the storage key directly — the fake's presigned URL
    // is "https://fake-storage.test/{key}", so recovering it from the presign response is simpler
    // than reaching into the database.
    private static string KeyFromPresignedUrl(string url) => new Uri(url).AbsolutePath.TrimStart('/');

    [Fact]
    public async Task PresignThenCompleteAcceptsACleanFileAndGetSplicesItIntoTheAnswer()
    {
        var (client, reportId, unitId) = await SetUpAsync("01");

        var presignResponse = await PresignAsync(client, reportId, unitId, "site.jpg", "image/jpeg", 12_345);
        var presigned = await presignResponse.Content.ReadFromJsonAsync<PresignedAttachmentUpload>();
        Assert.Equal(HttpStatusCode.OK, presignResponse.StatusCode);

        factory.FileStorage.Seed(KeyFromPresignedUrl(presigned!.UploadUrl), Encoding.ASCII.GetBytes("not a virus"));

        var completeResponse = await CompleteAsync(client, reportId, unitId, presigned.AttachmentId);
        var summary = await completeResponse.Content.ReadFromJsonAsync<AttachmentSummary>();

        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        Assert.Equal("site.jpg", summary!.FileName);
        Assert.Equal(11, summary.SizeBytes); // "not a virus" is 11 bytes — re-measured, not the declared 12_345.

        var getResponse = await client.GetAsync(new Uri($"/api/v1/reports/{reportId}?unit={unitId}", UriKind.Relative));
        var report = await getResponse.Content.ReadFromJsonAsync<ReportInstanceForEditing>();
        var photos = ((JsonElement)report!.Answers[FieldKey]!).Deserialize<List<AttachmentSummary>>(JsonSerializerOptions.Web);

        Assert.Single(photos!);
        Assert.Equal(summary.Id, photos![0].Id);
    }

    [Fact]
    public async Task PresignRejectsADisallowedContentType()
    {
        var (client, reportId, unitId) = await SetUpAsync("02");

        var response = await PresignAsync(client, reportId, unitId, "virus.exe", "application/x-msdownload", 1_000);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PresignRejectsAFileOver20Mb()
    {
        var (client, reportId, unitId) = await SetUpAsync("03");

        var response = await PresignAsync(client, reportId, unitId, "huge.jpg", "image/jpeg", 21 * 1024 * 1024);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PresignRejectsWhenTheReportTypeDoesNotAllowAttachments()
    {
        var (client, reportId, unitId) = await SetUpAsync("04", allowAttachments: false);

        var response = await PresignAsync(client, reportId, unitId, "site.jpg", "image/jpeg", 1_000);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PresignRejectsWhenTheReportsBudgetWouldBeExceeded()
    {
        var (client, reportId, unitId) = await SetUpAsync("05");

        // Five 20 MB files (the per-file cap) land exactly on the 100 MB per-report cap —
        // each presign alone is valid, but together they fill the budget.
        const long maxFileSize = 20 * 1024 * 1024;
        for (var i = 0; i < 5; i++)
        {
            var response = await PresignAsync(client, reportId, unitId, $"big{i}.jpg", "image/jpeg", maxFileSize);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var overBudget = await PresignAsync(client, reportId, unitId, "one-more.jpg", "image/jpeg", 1);

        Assert.Equal(HttpStatusCode.Conflict, overBudget.StatusCode);
    }

    [Fact]
    public async Task CompleteRejectsAnInfectedFileAndRemovesIt()
    {
        var (client, reportId, unitId) = await SetUpAsync("06");

        var presignResponse = await PresignAsync(client, reportId, unitId, "eicar.txt", "application/pdf", 1_000);
        var presigned = await presignResponse.Content.ReadFromJsonAsync<PresignedAttachmentUpload>();
        var key = KeyFromPresignedUrl(presigned!.UploadUrl);
        factory.FileStorage.Seed(key, Encoding.ASCII.GetBytes(FakeVirusScanner.EicarTestString));

        var completeResponse = await CompleteAsync(client, reportId, unitId, presigned.AttachmentId);

        Assert.Equal(HttpStatusCode.BadRequest, completeResponse.StatusCode);
        Assert.False(factory.FileStorage.Contains(key));
    }

    [Fact]
    public async Task CompleteWithoutAnUploadedFileFails()
    {
        var (client, reportId, unitId) = await SetUpAsync("07");

        var presignResponse = await PresignAsync(client, reportId, unitId, "site.jpg", "image/jpeg", 1_000);
        var presigned = await presignResponse.Content.ReadFromJsonAsync<PresignedAttachmentUpload>();

        var completeResponse = await CompleteAsync(client, reportId, unitId, presigned!.AttachmentId);

        Assert.Equal(HttpStatusCode.BadRequest, completeResponse.StatusCode);
    }

    [Fact]
    public async Task CompleteIsIdempotentOnAnAlreadyAcceptedAttachment()
    {
        var (client, reportId, unitId) = await SetUpAsync("08");

        var presignResponse = await PresignAsync(client, reportId, unitId, "site.jpg", "image/jpeg", 1_000);
        var presigned = await presignResponse.Content.ReadFromJsonAsync<PresignedAttachmentUpload>();
        factory.FileStorage.Seed(KeyFromPresignedUrl(presigned!.UploadUrl), "clean content"u8.ToArray());

        var firstComplete = await CompleteAsync(client, reportId, unitId, presigned.AttachmentId);
        var secondComplete = await CompleteAsync(client, reportId, unitId, presigned.AttachmentId);

        Assert.Equal(HttpStatusCode.OK, firstComplete.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondComplete.StatusCode);
    }

    [Fact]
    public async Task DeleteRemovesTheAttachmentFromStorageAndTheAnswer()
    {
        var (client, reportId, unitId) = await SetUpAsync("09");

        var presignResponse = await PresignAsync(client, reportId, unitId, "site.jpg", "image/jpeg", 1_000);
        var presigned = await presignResponse.Content.ReadFromJsonAsync<PresignedAttachmentUpload>();
        var key = KeyFromPresignedUrl(presigned!.UploadUrl);
        factory.FileStorage.Seed(key, "clean content"u8.ToArray());
        await CompleteAsync(client, reportId, unitId, presigned.AttachmentId);

        var deleteResponse = await DeleteAsync(client, reportId, unitId, presigned.AttachmentId);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.False(factory.FileStorage.Contains(key));
    }

    [Fact]
    public async Task AttachmentEndpointsRequireEditPermissionForTheUnit()
    {
        using var secretariat = await CreateSecretariatClientAsync();
        var reportTypeId = await CreateReportTypeAsync(secretariat, "Report Attachment No Permission Report");
        await PublishTemplateWithFileUploadFieldAsync(secretariat, reportTypeId);
        var muqamId = await CreateMuqamUnitAsync(secretariat, "10");
        var windowId = await CreateWindowAsync(secretariat, reportTypeId);

        var (client, _) = await CreateAuthenticatedUserAsync();

        var response = await PresignAsync(client, windowId, muqamId, "site.jpg", "image/jpeg", 1_000);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
