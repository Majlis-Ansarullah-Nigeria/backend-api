using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Domain.Access;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Infrastructure.Persistence;
using HRS.LoadTestSeeder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

/// <summary>M12.1: the k6 deadline-spike test's seeder (NFR-PRF-03) builds an organization a seeded
/// head can really sign in to and submit against, re-seeds without duplicating it, and cleans up.</summary>
[Collection(ApiTestCollection.Name)]
public class LoadTestSeederTests(ApiFactory factory)
{
    private const string Password = "LoadTest-Password-2026";

    [Fact]
    public void TheDefaultShapeIsFiveThousandUnitsSplitEvenly()
    {
        var shape = LoadTestShape.Default;

        Assert.Equal((10, 100, 4_890), (shape.Zones, shape.Dilaat, shape.Muqamaat));
        Assert.Equal(shape.Muqamaat, Enumerable.Range(0, shape.Dilaat).Sum(shape.MuqamaatUnder));
        Assert.Equal((49, 48), (shape.MuqamaatUnder(0), shape.MuqamaatUnder(99)));
        Assert.Throws<ArgumentException>(() => new LoadTestShape(2, 5, 10).Validate());
    }

    [Theory]
    [InlineData("Host=localhost;Port=5433;Database=hrs", true)]
    [InlineData("Host=127.0.0.1;Database=hrs", true)]
    [InlineData("Host=db.example.org;Database=hrs", false)]
    [InlineData("Host=localhost,db.example.org;Database=hrs", false)]
    public void TheSeederOnlyAcceptsALocalDatabase(string connectionString, bool expected) =>
        Assert.Equal(expected, LocalDatabaseGuard.IsLocal(connectionString));

    [Fact]
    public async Task ASeededHeadSignsInAndSubmitsAndCleanupRemovesEverything()
    {
        var shape = new LoadTestShape(1, 2, 7);
        LoadTestSeedResult first;
        LoadTestSeedResult second;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            first = await LoadTestOrganization.SeedAsync(db, shape, Password, DateTimeOffset.UtcNow);
        }

        try
        {
            Assert.Equal(7, first.Heads.Count);
            Assert.Equal(
                [(UnitLevelCode.Zone, 1), (UnitLevelCode.Dila, 2), (UnitLevelCode.Muqam, 4)],
                first.Heads.GroupBy(h => h.Level).Select(g => (g.Key, g.Count())).OrderBy(g => g.Key));
            Assert.Equal("email,unitId,unitCode,level,windowId", LoadTestCsv.Heads(first).Split('\n')[0]);

            var muqam = first.Heads.First(h => h.Level == UnitLevelCode.Muqam);
            var head = factory.CreateAuthenticatedClient();
            var login = await head.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(muqam.Email, Password));
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            var url = $"/api/v1/reports/{first.WindowId}?unit={muqam.UnitId}";
            var current = await head.GetAsync(new Uri(url, UriKind.Relative));
            Assert.Equal(HttpStatusCode.OK, current.StatusCode);
            using (var save = new HttpRequestMessage(HttpMethod.Put, new Uri(url, UriKind.Relative))
            {
                Content = JsonContent.Create(new SaveReportDraftRequest(new() { ["members"] = 120, ["meetings"] = 4, ["funds"] = 50_000, ["highlights"] = "Steady." })),
            })
            {
                if (current.Headers.ETag is { } etag)
                {
                    save.Headers.TryAddWithoutValidation("If-Match", etag.Tag);
                }

                Assert.True((await head.SendAsync(save)).IsSuccessStatusCode);
            }

            var submitted = await head.PostAsJsonAsync($"/api/v1/reports/{first.WindowId}/submit?unit={muqam.UnitId}", new SubmitReportRequest(Confirmed: true));
            Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);

            var secretariat = factory.CreateAuthenticatedClient();
            Assert.Equal(HttpStatusCode.OK,
                (await secretariat.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(first.SecretariatEmail, Password))).StatusCode);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Equal(ReportInstanceStatus.Submitted,
                    (await db.ReportInstances.AsNoTracking().SingleAsync(i => i.WindowId == first.WindowId && i.UnitId == muqam.UnitId)).Status);

                // Re-seeding reuses the organization and users but opens a fresh window.
                second = await LoadTestOrganization.SeedAsync(db, shape, Password, DateTimeOffset.UtcNow);
                Assert.NotEqual(first.WindowId, second.WindowId);
                // The last run's window goes, so this run's reference numbers can't collide with it.
                Assert.False(await db.SubmissionWindows.AnyAsync(w => w.Id == first.WindowId));
                Assert.False(await db.ReportInstances.AnyAsync(i => i.WindowId == first.WindowId));
                Assert.Equal(first.Heads.Select(h => h.UnitId), second.Heads.Select(h => h.UnitId));
                Assert.Equal(7, await db.RoleAssignments.CountAsync(a =>
                    a.Role == RoleName.UnitHead && first.Heads.Select(h => h.UnitId).Contains(a.UnitId)));
            }
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await LoadTestOrganization.CleanupAsync(db);
        }

        using var check = factory.Services.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await checkDb.Units.AnyAsync(u => u.Path.StartsWith("nat.lt")));
        Assert.False(await checkDb.Users.AnyAsync(u => u.Email!.EndsWith("@" + LoadTestOrganization.EmailDomain)));
        Assert.False(await checkDb.ReportTypes.AnyAsync(r => r.Name == LoadTestOrganization.ReportTypeName));
        Assert.False(await checkDb.SubmissionWindows.AnyAsync(w => w.Id == first.WindowId || w.Id == second.WindowId));
    }
}
