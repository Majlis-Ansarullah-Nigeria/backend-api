using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;
using HRS.Api.Endpoints;
using HRS.Application.Features.Organization.BulkImport;
using HRS.Application.Features.Organization.Units;
using HRS.Domain.Access;
using HRS.Domain.Organization;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class BulkImportEndpointsTests(ApiFactory factory)
{
    private const string Header = "Level,Code,Name,Parent Code,Address,Contact Phone,Contact Email,Head Name,Head Email";

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static string UniqueZoneCode() => $"Z{Guid.NewGuid():N}"[..6].ToUpperInvariant();

    private async Task<HttpClient> CreateOrgAdminClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Imp1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<HttpClient> CreatePlainUserClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Imp2";
        await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private static MultipartFormDataContent Csv(params string[] dataRows)
    {
        var csv = string.Join('\n', [Header, .. dataRows]);
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "units.csv");
        return content;
    }

    private static async Task<BulkImportSummary> PreviewAsync(HttpClient client, HttpContent content)
    {
        var response = await client.PostAsync(new Uri("/api/v1/units/bulk-import/preview", UriKind.Relative), content);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BulkImportSummary>())!;
    }

    [Fact]
    public async Task BulkImportEndpointsRequireOrgAdmin()
    {
        using var client = await CreatePlainUserClientAsync();

        var template = await client.GetAsync(new Uri("/api/v1/units/bulk-import/template", UriKind.Relative));
        var preview = await client.PostAsync(new Uri("/api/v1/units/bulk-import/preview", UriKind.Relative), Csv());
        var commit = await client.PostAsync(new Uri("/api/v1/units/bulk-import/commit", UriKind.Relative), Csv());

        Assert.Equal(HttpStatusCode.Forbidden, template.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, preview.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, commit.StatusCode);
    }

    [Fact]
    public async Task DownloadTemplateReturnsAWorkbookWithTheExpectedHeaders()
    {
        using var client = await CreateOrgAdminClientAsync();

        var response = await client.GetAsync(new Uri("/api/v1/units/bulk-import/template", UriKind.Relative));
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var headerRow = workbook.Worksheets.First().Row(1);
        var headers = headerRow.Cells(1, BulkImportColumns.All.Count).Select(c => c.GetString()).ToList();
        Assert.Equal(BulkImportColumns.All, headers);
    }

    [Fact]
    public async Task PreviewClassifiesANewUnitWithAHeadAsNew()
    {
        using var client = await CreateOrgAdminClientAsync();
        var code = UniqueZoneCode();

        var summary = await PreviewAsync(client, Csv($"Zone,{code},Test Zone,,,,,Head Person,head@example.org"));

        var row = Assert.Single(summary.Rows);
        Assert.Equal(BulkImportOutcome.New, row.Outcome);
        Assert.Null(row.Issue);
        Assert.Equal(1, summary.NewCount);
    }

    [Fact]
    public async Task PreviewClassifiesABlankHeadAsWarning()
    {
        using var client = await CreateOrgAdminClientAsync();
        var code = UniqueZoneCode();

        var summary = await PreviewAsync(client, Csv($"Zone,{code},Test Zone,,,,,,"));

        var row = Assert.Single(summary.Rows);
        Assert.Equal(BulkImportOutcome.Warning, row.Outcome);
        Assert.Equal("No head; unit will be vacant.", row.Issue);
    }

    [Fact]
    public async Task PreviewFlagsAnUnresolvableParentCodeAsAnError()
    {
        using var client = await CreateOrgAdminClientAsync();

        var summary = await PreviewAsync(client, Csv("Muqam,ZZ-ZZ-ZZZ,Ghost Muqam,ZZ-ZZ,,,,,"));

        var row = Assert.Single(summary.Rows);
        Assert.Equal(BulkImportOutcome.Error, row.Outcome);
        Assert.Contains("not found", row.Issue, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, summary.ErrorCount);
    }

    [Fact]
    public async Task PreviewFlagsAMalformedHeadEmailAsAnError()
    {
        using var client = await CreateOrgAdminClientAsync();
        var code = UniqueZoneCode();

        var summary = await PreviewAsync(client, Csv($"Zone,{code},Test Zone,,,,,Someone,not-an-email"));

        var row = Assert.Single(summary.Rows);
        Assert.Equal(BulkImportOutcome.Error, row.Outcome);
        Assert.Equal("Invalid email.", row.Issue);
    }

    [Fact]
    public async Task PreviewFlagsADuplicateCodeWithinTheFileAsAnError()
    {
        using var client = await CreateOrgAdminClientAsync();
        var code = UniqueZoneCode();

        var summary = await PreviewAsync(client, Csv(
            $"Zone,{code},Test Zone,,,,,,",
            $"Zone,{code},Test Zone Again,,,,,,"));

        Assert.Equal(1, summary.ErrorCount);
        Assert.Contains(summary.Rows, r => r.Outcome == BulkImportOutcome.Error && r.Issue == "Duplicate code in file.");
    }

    [Fact]
    public async Task PreviewResolvesAParentCreatedEarlierInTheSameBatchEvenIfItAppearsLaterInTheFile()
    {
        using var client = await CreateOrgAdminClientAsync();
        var zoneCode = UniqueZoneCode();
        var dilaCode = $"{zoneCode}-D1";

        // Dila row lists a Parent Code for a Zone that doesn't exist yet — only appears
        // further down in the same file. Depth-ordering must resolve the Zone first.
        // (Head Email is filled in on both so they classify as New, not Warning, keeping
        // this test's assertion focused on depth ordering rather than head classification.)
        var summary = await PreviewAsync(client, Csv(
            $"Dila,{dilaCode},Test Dila,{zoneCode},,,,Dila Head,{UniqueEmail()}",
            $"Zone,{zoneCode},Test Zone,,,,,Zone Head,{UniqueEmail()}"));

        var issues = string.Join(" | ", summary.Rows.Select(r => $"{r.Code}:{r.Outcome}:{r.Issue}"));
        Assert.True(summary.ErrorCount == 0, issues);
        Assert.Equal(2, summary.NewCount);
    }

    [Fact]
    public async Task CommitCreatesNewUnitsAndSkipsErrorRows()
    {
        using var client = await CreateOrgAdminClientAsync();
        var goodCode = UniqueZoneCode();

        var response = await client.PostAsync(
            new Uri("/api/v1/units/bulk-import/commit", UriKind.Relative),
            Csv(
                $"Zone,{goodCode},Test Zone,,,,,,",
                "Muqam,ZZ-ZZ-ZZZ,Ghost Muqam,ZZ-ZZ,,,,,"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BulkImportCommitResult>();
        Assert.Equal(1, result!.CreatedCount);
        Assert.Equal(1, result.SkippedErrorCount);

        var tree = await (await client.GetAsync(new Uri("/api/v1/units/tree", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<UnitTreeNode>>();
        Assert.Contains(tree!, u => u.Code == goodCode);
    }

    [Fact]
    public async Task CommitIsIdempotentOnReimport()
    {
        using var client = await CreateOrgAdminClientAsync();
        var code = UniqueZoneCode();
        var content = () => Csv($"Zone,{code},Test Zone,,,,,,");

        await client.PostAsync(new Uri("/api/v1/units/bulk-import/commit", UriKind.Relative), content());
        var second = await client.PostAsync(new Uri("/api/v1/units/bulk-import/commit", UriKind.Relative), content());
        var secondResult = await second.Content.ReadFromJsonAsync<BulkImportCommitResult>();

        Assert.Equal(0, secondResult!.CreatedCount);
        Assert.Equal(1, secondResult.UpdatedCount);

        var tree = await (await client.GetAsync(new Uri("/api/v1/units/tree", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<UnitTreeNode>>();
        Assert.Single(tree!, u => u.Code == code);
    }

    [Fact]
    public async Task CommitAssignsTheHeadWhenTheEmailBelongsToAnExistingUser()
    {
        using var client = await CreateOrgAdminClientAsync();
        var code = UniqueZoneCode();
        var headEmail = UniqueEmail();
        await factory.CreateUserAsync(headEmail, "Correct-Horse-Head1", name: "Amina Yusuf");

        await client.PostAsync(
            new Uri("/api/v1/units/bulk-import/commit", UriKind.Relative),
            Csv($"Zone,{code},Test Zone,,,,,Amina,{headEmail}"));

        var tree = await (await client.GetAsync(new Uri("/api/v1/units/tree", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<UnitTreeNode>>();
        var unit = tree!.Single(u => u.Code == code);
        Assert.False(unit.HasVacantHead);
        Assert.Equal("Amina Yusuf", unit.HeadName);
    }

    [Fact]
    public async Task CommitInvitesAHeadWhoseEmailIsntRegisteredYet()
    {
        using var client = await CreateOrgAdminClientAsync();
        var code = UniqueZoneCode();
        var headEmail = UniqueEmail();

        var response = await client.PostAsync(
            new Uri("/api/v1/units/bulk-import/commit", UriKind.Relative),
            Csv($"Zone,{code},Test Zone,,,,,New Head,{headEmail}"));
        var result = await response.Content.ReadFromJsonAsync<BulkImportCommitResult>();

        Assert.Equal(1, result!.InvitedCount);
        Assert.NotNull(factory.EmailSender.LastSentTo(headEmail));
    }

    [Fact]
    public async Task CommitNeverReplacesAnExistingActiveHead()
    {
        using var client = await CreateOrgAdminClientAsync();
        var created = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest(UniqueZoneCode(), "Headed Zone", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();

        var originalHeadId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Head2", name: "Original Head");
        await factory.CreateRoleAssignmentAsync(originalHeadId, created!.Id, RoleName.UnitHead);

        var newHeadEmail = UniqueEmail();
        var preview = await PreviewAsync(
            client, Csv($"Zone,{created.Code},Headed Zone,,,,,New Head,{newHeadEmail}"));
        var row = Assert.Single(preview.Rows);
        Assert.Equal(BulkImportOutcome.Warning, row.Outcome);
        Assert.Equal("Unit already has a head; not changed by import.", row.Issue);

        await client.PostAsync(
            new Uri("/api/v1/units/bulk-import/commit", UriKind.Relative),
            Csv($"Zone,{created.Code},Headed Zone,,,,,New Head,{newHeadEmail}"));

        var tree = await (await client.GetAsync(new Uri("/api/v1/units/tree", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<UnitTreeNode>>();
        var unit = tree!.Single(u => u.Id == created.Id);
        Assert.Equal("Original Head", unit.HeadName);
        Assert.Null(factory.EmailSender.LastSentTo(newHeadEmail));
    }

    [Fact]
    public async Task CommitFillsAVacancyOnAnExistingUnitWithNoHead()
    {
        using var client = await CreateOrgAdminClientAsync();
        var created = await (await client.PostAsJsonAsync(
                "/api/v1/units",
                new CreateUnitRequest(UniqueZoneCode(), "Vacant Zone", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();

        var headEmail = UniqueEmail();
        await factory.CreateUserAsync(headEmail, "Correct-Horse-Head3", name: "Filled Head");

        await client.PostAsync(
            new Uri("/api/v1/units/bulk-import/commit", UriKind.Relative),
            Csv($"Zone,{created!.Code},Vacant Zone,,,,,Filled,{headEmail}"));

        var tree = await (await client.GetAsync(new Uri("/api/v1/units/tree", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<UnitTreeNode>>();
        var unit = tree!.Single(u => u.Id == created.Id);
        Assert.False(unit.HasVacantHead);
        Assert.Equal("Filled Head", unit.HeadName);
    }

    [Fact]
    public async Task PreviewFlagsAFileWithoutTheRequiredColumnsAsAnError()
    {
        using var client = await CreateOrgAdminClientAsync();
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Foo,Bar\n1,2"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "bad.csv");

        var response = await client.PostAsync(new Uri("/api/v1/units/bulk-import/preview", UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
