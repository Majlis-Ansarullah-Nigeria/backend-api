using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Audit;
using HRS.Application.Features.Organization.Units;
using HRS.Domain.Access;
using HRS.Domain.Audit;
using HRS.Domain.Organization;
using HRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

/// <summary>FR-AUD-001 (append-only audit log written alongside each change) and FR-AUD-002
/// (viewer with filters and CSV export for the Secretariat and the system administrator).</summary>
[Collection(ApiTestCollection.Name)]
public class AuditEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private async Task<(HttpClient Client, Guid UserId, string Email)> CreateClientAsync(RoleName? role, Guid unitId)
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Audit1";
        var userId = await factory.CreateUserAsync(email, password);
        if (role is { } r)
        {
            await factory.CreateRoleAssignmentAsync(userId, unitId, r);
        }

        var client = factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AuditTest/1.0");
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        return (client, userId, email);
    }

    private async Task<List<AuditEvent>> EventsAsync(Func<AuditEvent, bool> predicate)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return [.. (await db.AuditEvents.AsNoTracking().ToListAsync()).Where(predicate)];
    }

    [Fact]
    public async Task SignInsAndFailedSignInsAreRecorded()
    {
        var (_, userId, email) = await CreateClientAsync(null, OrganizationSeedIds.NationalUnitId);
        var stranger = factory.CreateAuthenticatedClient();
        var wrongEmail = UniqueEmail();
        await stranger.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(wrongEmail, "Not-The-Password1"));

        var signedIn = Assert.Single(await EventsAsync(e => e.ActorUserId == userId && e.Action == AuditActions.SignedIn));
        Assert.Equal("AuditTest/1.0", signedIn.UserAgent);
        var failed = Assert.Single(await EventsAsync(e => e.Action == AuditActions.SignInFailed && e.ActorEmail == wrongEmail));
        Assert.Null(failed.ActorUserId);
        Assert.DoesNotContain("Not-The-Password1", failed.Summary + failed.AfterJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AUnitChangeIsRecordedWithItsUnitAndFoundThroughItsParentInTheViewer()
    {
        var (secretariat, secretaryId, _) = await CreateClientAsync(RoleName.Secretariat, OrganizationSeedIds.NationalUnitId);
        var zone = await (await secretariat.PostAsJsonAsync("/api/v1/units",
                new CreateUnitRequest("AUZ01", "Audit Zone 01", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dila = await (await secretariat.PostAsJsonAsync("/api/v1/units",
                new CreateUnitRequest("AUD01", "Audit Dila 01", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        await secretariat.PutAsJsonAsync($"/api/v1/units/{dila!.Id}",
            new UpdateUnitRequest("Audit Dila 01 (renamed)", null, null, null, null, null));

        var page = await secretariat.GetFromJsonAsync<AuditEventPage>(
            $"/api/v1/audit-events?unitId={zone.Id}&actorUserId={secretaryId}");

        // Newest first; the unit column shows each unit's current name.
        Assert.Equal(
            [
                (AuditActions.UnitUpdated, (string?)"Audit Dila 01 (renamed)"),
                (AuditActions.UnitCreated, (string?)"Audit Dila 01 (renamed)"),
                (AuditActions.UnitCreated, (string?)"Audit Zone 01"),
            ],
            page!.Rows.Select(r => (r.Action, r.UnitName)));
        var rename = page.Rows[0];
        using var before = System.Text.Json.JsonDocument.Parse(rename.BeforeJson!);
        using var after = System.Text.Json.JsonDocument.Parse(rename.AfterJson!);
        Assert.Equal("Audit Dila 01", before.RootElement.GetProperty("name").GetString());
        Assert.Equal("Audit Dila 01 (renamed)", after.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task TheViewerMasksIpsTheCsvKeepsThemAndExportingIsItselfAudited()
    {
        var (secretariat, secretaryId, _) = await CreateClientAsync(RoleName.Secretariat, OrganizationSeedIds.NationalUnitId);
        var marker = $"Seeded entry {Guid.NewGuid():N}";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditEvents.Add(AuditEvent.Create(
                Guid.CreateVersion7(), DateTimeOffset.UtcNow, AuditActions.WindowExtended, secretaryId, null, "window", null, null,
                marker, null, null, "102.89.12.34", "Browser"));
            await db.SaveChangesAsync();
        }

        var page = await secretariat.GetFromJsonAsync<AuditEventPage>($"/api/v1/audit-events?action={AuditActions.WindowExtended}&actorUserId={secretaryId}");
        var csv = await secretariat.GetAsync(new Uri($"/api/v1/audit-events/csv?action={AuditActions.WindowExtended}&actorUserId={secretaryId}", UriKind.Relative));
        var text = await csv.Content.ReadAsStringAsync();

        Assert.Equal("102.89.x.x", Assert.Single(page!.Rows, r => r.Summary == marker).IpAddress);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        Assert.Contains(marker, text, StringComparison.Ordinal);
        Assert.Contains("102.89.12.34", text, StringComparison.Ordinal);
        Assert.Single(await EventsAsync(e => e.ActorUserId == secretaryId && e.Action == AuditActions.AuditExported));
    }

    [Fact]
    public async Task OnlyTheSecretariatAndSystemAdministratorSeeTheLog()
    {
        var head = (await CreateClientAsync(RoleName.UnitHead, OrganizationSeedIds.NationalUnitId)).Client;
        var leadership = (await CreateClientAsync(RoleName.Leadership, OrganizationSeedIds.NationalUnitId)).Client;
        var admin = (await CreateClientAsync(RoleName.SysAdmin, OrganizationSeedIds.NationalUnitId)).Client;

        Assert.Equal(HttpStatusCode.Forbidden, (await head.GetAsync(new Uri("/api/v1/audit-events", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await leadership.GetAsync(new Uri("/api/v1/audit-events/csv", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(new Uri("/api/v1/audit-events/filters", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task TheDatabaseRefusesToChangeOrDeleteEntries()
    {
        var (_, userId, _) = await CreateClientAsync(null, OrganizationSeedIds.NationalUnitId);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var update = await Record.ExceptionAsync(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE audit_events SET summary = 'tampered' WHERE actor_user_id = {userId}"));
        var delete = await Record.ExceptionAsync(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM audit_events WHERE actor_user_id = {userId}"));

        Assert.Contains("append-only", update?.Message, StringComparison.Ordinal);
        Assert.Contains("append-only", delete?.Message, StringComparison.Ordinal);
    }
}
