using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Templates;

namespace HRS.Api.IntegrationTests;

/// <summary>Locks in the organization's seeded "Dila Monthly Activities Report" (migration
/// SeedDefaultDilaMonthlyReportTemplate) — a future migration touching report-type data should not
/// silently break or orphan it.</summary>
[Collection(ApiTestCollection.Name)]
public class DefaultTemplateSeedTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private async Task<HttpClient> CreateSecretariatClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Seed1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    [Fact]
    public async Task TheDefaultDilaMonthlyReportIsSeededAndPublished()
    {
        using var client = await CreateSecretariatClientAsync();

        var reportTypes = await (await client.GetAsync(new Uri("/api/v1/report-types", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<ReportTypeSummary>>();

        var seeded = reportTypes!.Single(r => r.Name == "Dila Monthly Activities Report");
        Assert.Null(seeded.ArchivedAt);
        Assert.Contains(UnitLevelCode.Dila, seeded.ApplicableLevels);
        Assert.True(seeded.AllowNilReport);

        var versions = await (await client.GetAsync(new Uri($"/api/v1/report-types/{seeded.Id}/template-versions", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<TemplateVersionSummary>>();
        var publishedSummary = versions!.Single(v => v.Status == TemplateVersionStatus.Published);

        var published = await (await client.GetAsync(
                new Uri($"/api/v1/report-types/{seeded.Id}/template-versions/{publishedSummary.Id}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<TemplateVersionDetail>();

        Assert.Equal(13, published!.Schema.Sections.Count);
        Assert.Contains(published.Schema.Sections, s => s.Key == "umumi");
        Assert.Contains(published.Schema.Sections, s => s.Key == "mal");

        var allFieldKeys = published.Schema.Sections.SelectMany(s => s.Fields).Select(f => f.Key).ToList();
        Assert.Equal(allFieldKeys.Count, allFieldKeys.Distinct().Count());
    }
}
