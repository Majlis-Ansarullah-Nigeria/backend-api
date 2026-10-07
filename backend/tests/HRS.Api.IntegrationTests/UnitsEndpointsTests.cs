using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Organization.Levels;
using HRS.Application.Features.Organization.Units;
using HRS.Domain.Access;
using HRS.Domain.Organization;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class UnitsEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static CreateUnitRequest ZoneRequest(string code = "Z01", string name = "Zone 1") =>
        new(code, name, OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null);

    private async Task<HttpClient> CreateOrgAdminClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Org1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<HttpClient> CreatePlainUserClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Org2";
        await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    [Fact]
    public async Task CreatingAUnitRequiresOrgAdmin()
    {
        using var client = await CreatePlainUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/units", ZoneRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrgAdminCanCreateAZoneUnderNational()
    {
        using var client = await CreateOrgAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/units", ZoneRequest("Z02", "Zone 2"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var zone = await response.Content.ReadFromJsonAsync<UnitSummary>();
        Assert.Equal("Z02", zone!.Code);
        Assert.Equal("nat.z02", zone.Path);
        Assert.Equal(OrganizationSeedIds.NationalUnitId, zone.ParentId);
        Assert.Equal(UnitLevelCode.Zone, zone.LevelCode);
    }

    [Fact]
    public async Task CreatingAUnitWithAMismatchedParentLevelFails()
    {
        using var client = await CreateOrgAdminClientAsync();

        // A Dila can't be created directly under National — it must be under a Zone.
        var request = new CreateUnitRequest(
            "D99", "Dila 99", OrganizationSeedIds.DilaLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null);

        var response = await client.PostAsJsonAsync("/api/v1/units", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatingADuplicateCodeUnderTheSameParentFails()
    {
        using var client = await CreateOrgAdminClientAsync();
        await client.PostAsJsonAsync("/api/v1/units", ZoneRequest("Z03", "Zone 3"));

        var response = await client.PostAsJsonAsync("/api/v1/units", ZoneRequest("z03", "Zone 3 Duplicate"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreatingAUnitUnderAnArchivedParentFails()
    {
        using var client = await CreateOrgAdminClientAsync();
        var zoneResponse = await client.PostAsJsonAsync("/api/v1/units", ZoneRequest("Z04", "Zone 4"));
        var zone = await zoneResponse.Content.ReadFromJsonAsync<UnitSummary>();
        await client.DeleteAsync(new Uri($"/api/v1/units/{zone!.Id}", UriKind.Relative));

        var dilaRequest = new CreateUnitRequest("D01", "Dila 1", OrganizationSeedIds.DilaLevelId, zone.Id, null, null, null, null, null);
        var response = await client.PostAsJsonAsync("/api/v1/units", dilaRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetUnitReturnsTheCreatedUnit()
    {
        using var client = await CreateOrgAdminClientAsync();
        var created = await (await client.PostAsJsonAsync("/api/v1/units", ZoneRequest("Z05", "Zone 5")))
            .Content.ReadFromJsonAsync<UnitSummary>();

        var response = await client.GetAsync(new Uri($"/api/v1/units/{created!.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var unit = await response.Content.ReadFromJsonAsync<UnitSummary>();
        Assert.Equal("Z05", unit!.Code);
        Assert.Equal("Z05", unit.DisplayCode);
    }

    [Fact]
    public async Task GetUnitForAnUnknownIdReturnsNotFound()
    {
        using var client = await CreateOrgAdminClientAsync();

        var response = await client.GetAsync(new Uri($"/api/v1/units/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetUnitsFiltersByParentId()
    {
        using var client = await CreateOrgAdminClientAsync();
        var zone = await (await client.PostAsJsonAsync("/api/v1/units", ZoneRequest("Z06", "Zone 6")))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dilaRequest = new CreateUnitRequest("D02", "Dila 2", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null);
        await client.PostAsJsonAsync("/api/v1/units", dilaRequest);

        var response = await client.GetAsync(new Uri($"/api/v1/units?parentId={zone.Id}", UriKind.Relative));
        var units = await response.Content.ReadFromJsonAsync<List<UnitSummary>>();

        var dila = Assert.Single(units!);
        Assert.Equal("D02", dila.Code);
    }

    [Fact]
    public async Task GetUnitsSearchesByName()
    {
        using var client = await CreateOrgAdminClientAsync();
        await client.PostAsJsonAsync("/api/v1/units", ZoneRequest("Z07", "Searchable Zone Seven"));

        var response = await client.GetAsync(new Uri("/api/v1/units?q=searchable%20zone", UriKind.Relative));
        var units = await response.Content.ReadFromJsonAsync<List<UnitSummary>>();

        Assert.Contains(units!, u => u.Code == "Z07");
    }

    [Fact]
    public async Task UpdateUnitChangesNameAndContactButNotCodeOrPath()
    {
        using var client = await CreateOrgAdminClientAsync();
        var zone = await (await client.PostAsJsonAsync("/api/v1/units", ZoneRequest("Z08", "Zone 8")))
            .Content.ReadFromJsonAsync<UnitSummary>();

        var update = new UpdateUnitRequest("Zone Eight Renamed", "1 Main St", "+2348030000000", "zone8@example.org", 6.5, 3.4);
        var response = await client.PutAsJsonAsync($"/api/v1/units/{zone!.Id}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UnitSummary>();
        Assert.Equal("Zone Eight Renamed", updated!.Name);
        Assert.Equal("Z08", updated.Code);
        Assert.Equal("nat.z08", updated.Path);
        Assert.Equal("1 Main St", updated.Address);
        Assert.Equal(6.5, updated.Latitude);
    }

    [Fact]
    public async Task ArchiveThenRestoreRoundTrips()
    {
        using var client = await CreateOrgAdminClientAsync();
        var zone = await (await client.PostAsJsonAsync("/api/v1/units", ZoneRequest("Z09", "Zone 9")))
            .Content.ReadFromJsonAsync<UnitSummary>();

        var archive = await client.DeleteAsync(new Uri($"/api/v1/units/{zone!.Id}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var afterArchive = await (await client.GetAsync(new Uri($"/api/v1/units/{zone.Id}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        Assert.NotNull(afterArchive!.ArchivedAt);

        var defaultList = await (await client.GetAsync(new Uri($"/api/v1/units?parentId={OrganizationSeedIds.NationalUnitId}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<UnitSummary>>();
        Assert.DoesNotContain(defaultList!, u => u.Id == zone.Id);

        var includeArchivedList = await (await client.GetAsync(
                new Uri($"/api/v1/units?parentId={OrganizationSeedIds.NationalUnitId}&includeArchived=true", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<UnitSummary>>();
        Assert.Contains(includeArchivedList!, u => u.Id == zone.Id);

        var restore = await client.PostAsync(new Uri($"/api/v1/units/{zone.Id}/restore", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.NoContent, restore.StatusCode);

        var afterRestore = await (await client.GetAsync(new Uri($"/api/v1/units/{zone.Id}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        Assert.Null(afterRestore!.ArchivedAt);
    }

    [Fact]
    public async Task ArchivingTheNationalRootIsRejected()
    {
        using var client = await CreateOrgAdminClientAsync();

        var response = await client.DeleteAsync(new Uri($"/api/v1/units/{OrganizationSeedIds.NationalUnitId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetUnitLevelsReturnsAllFiveLevelsOrderedByDepth()
    {
        using var client = await CreatePlainUserClientAsync();

        var response = await client.GetAsync(new Uri("/api/v1/unit-levels", UriKind.Relative));
        var levels = await response.Content.ReadFromJsonAsync<List<UnitLevelSummary>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, levels!.Count);
        Assert.Equal(UnitLevelCode.National, levels[0].Code);
        Assert.True(levels.SequenceEqual(levels.OrderBy(l => l.Depth)));
    }

    [Fact]
    public async Task GetOrganizationTreeMarksANewlyCreatedUnitAsVacant()
    {
        using var client = await CreateOrgAdminClientAsync();
        var zone = await (await client.PostAsJsonAsync("/api/v1/units", ZoneRequest("Z10", "Zone 10")))
            .Content.ReadFromJsonAsync<UnitSummary>();

        var response = await client.GetAsync(new Uri("/api/v1/units/tree", UriKind.Relative));
        var tree = await response.Content.ReadFromJsonAsync<List<UnitTreeNode>>();

        var node = Assert.Single(tree!, n => n.Id == zone!.Id);
        Assert.True(node.HasVacantHead);
        Assert.Null(node.HeadName);
    }

    [Fact]
    public async Task GetOrganizationTreeShowsTheActiveHeadsName()
    {
        using var client = await CreateOrgAdminClientAsync();
        var zone = await (await client.PostAsJsonAsync("/api/v1/units", ZoneRequest("Z11", "Zone 11")))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var headUserId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Head1", name: "Amina Yusuf");
        await factory.CreateRoleAssignmentAsync(headUserId, zone!.Id, RoleName.UnitHead);

        var response = await client.GetAsync(new Uri("/api/v1/units/tree", UriKind.Relative));
        var tree = await response.Content.ReadFromJsonAsync<List<UnitTreeNode>>();

        var node = Assert.Single(tree!, n => n.Id == zone.Id);
        Assert.False(node.HasVacantHead);
        Assert.Equal("Amina Yusuf", node.HeadName);
    }

    [Fact]
    public async Task GetOrganizationTreeExcludesArchivedUnitsUnlessIncluded()
    {
        using var client = await CreateOrgAdminClientAsync();
        var zone = await (await client.PostAsJsonAsync("/api/v1/units", ZoneRequest("Z12", "Zone 12")))
            .Content.ReadFromJsonAsync<UnitSummary>();
        await client.DeleteAsync(new Uri($"/api/v1/units/{zone!.Id}", UriKind.Relative));

        var defaultTree = await (await client.GetAsync(new Uri("/api/v1/units/tree", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<UnitTreeNode>>();
        Assert.DoesNotContain(defaultTree!, n => n.Id == zone.Id);

        var fullTree = await (await client.GetAsync(new Uri("/api/v1/units/tree?includeArchived=true", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<UnitTreeNode>>();
        Assert.Contains(fullTree!, n => n.Id == zone.Id);
    }

    private static async Task<(UnitSummary Zone, UnitSummary Dila)> CreateZoneWithDilaAsync(HttpClient client, string zoneCode, string dilaCode)
    {
        var zone = await (await client.PostAsJsonAsync("/api/v1/units", ZoneRequest(zoneCode, $"Zone {zoneCode}")))
            .Content.ReadFromJsonAsync<UnitSummary>();
        var dilaRequest = new CreateUnitRequest(
            dilaCode, $"Dila {dilaCode}", OrganizationSeedIds.DilaLevelId, zone!.Id, null, null, null, null, null);
        var dila = await (await client.PostAsJsonAsync("/api/v1/units", dilaRequest)).Content.ReadFromJsonAsync<UnitSummary>();

        return (zone, dila!);
    }

    [Fact]
    public async Task MovingAUnitRequiresOrgAdmin()
    {
        using var adminClient = await CreateOrgAdminClientAsync();
        var (_, dila) = await CreateZoneWithDilaAsync(adminClient, "M01", "D10");
        var (otherZone, _) = await CreateZoneWithDilaAsync(adminClient, "M02", "D11");

        using var plainClient = await CreatePlainUserClientAsync();
        var response = await plainClient.PostAsJsonAsync($"/api/v1/units/{dila.Id}/move", new MoveUnitRequest(otherZone.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MovingADilaToAnotherZoneUpdatesItsPathAndParent()
    {
        using var client = await CreateOrgAdminClientAsync();
        var (_, dila) = await CreateZoneWithDilaAsync(client, "M03", "D12");
        var (otherZone, _) = await CreateZoneWithDilaAsync(client, "M04", "D13");

        var response = await client.PostAsJsonAsync($"/api/v1/units/{dila.Id}/move", new MoveUnitRequest(otherZone.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var moved = await response.Content.ReadFromJsonAsync<UnitSummary>();
        Assert.Equal(otherZone.Id, moved!.ParentId);
        Assert.Equal($"{otherZone.Path}.d12", moved.Path);
    }

    [Fact]
    public async Task MovingAUnitRebasesItsDescendantsPaths()
    {
        using var client = await CreateOrgAdminClientAsync();
        var (_, dila) = await CreateZoneWithDilaAsync(client, "M05", "D14");
        var muqamRequest = new CreateUnitRequest(
            "MQ01", "Muqam 1", OrganizationSeedIds.MuqamLevelId, dila.Id, null, null, null, null, null);
        var muqam = await (await client.PostAsJsonAsync("/api/v1/units", muqamRequest)).Content.ReadFromJsonAsync<UnitSummary>();
        var (otherZone, _) = await CreateZoneWithDilaAsync(client, "M06", "D15");

        await client.PostAsJsonAsync($"/api/v1/units/{dila.Id}/move", new MoveUnitRequest(otherZone.Id));

        var movedMuqam = await (await client.GetAsync(new Uri($"/api/v1/units/{muqam!.Id}", UriKind.Relative)))
            .Content.ReadFromJsonAsync<UnitSummary>();
        Assert.Equal($"{otherZone.Path}.d14.mq01", movedMuqam!.Path);
    }

    [Fact]
    public async Task MovingAUnitIntoItsOwnDescendantIsRejected()
    {
        using var client = await CreateOrgAdminClientAsync();
        var (zone, dila) = await CreateZoneWithDilaAsync(client, "M07", "D16");

        var response = await client.PostAsJsonAsync($"/api/v1/units/{zone.Id}/move", new MoveUnitRequest(dila.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MovingAUnitToAMismatchedParentLevelFails()
    {
        using var client = await CreateOrgAdminClientAsync();
        var (zone, dila) = await CreateZoneWithDilaAsync(client, "M08", "D17");

        // A Zone can only go under National — not under a Dila.
        var response = await client.PostAsJsonAsync($"/api/v1/units/{zone.Id}/move", new MoveUnitRequest(dila.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MovingAUnitUnderAnArchivedParentFails()
    {
        using var client = await CreateOrgAdminClientAsync();
        var (_, dila) = await CreateZoneWithDilaAsync(client, "M09", "D18");
        var (otherZone, _) = await CreateZoneWithDilaAsync(client, "M10", "D19");
        await client.DeleteAsync(new Uri($"/api/v1/units/{otherZone.Id}", UriKind.Relative));

        var response = await client.PostAsJsonAsync($"/api/v1/units/{dila.Id}/move", new MoveUnitRequest(otherZone.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task MovingAUnitToItsCurrentParentIsRejected()
    {
        using var client = await CreateOrgAdminClientAsync();
        var (zone, dila) = await CreateZoneWithDilaAsync(client, "M11", "D20");

        var response = await client.PostAsJsonAsync($"/api/v1/units/{dila.Id}/move", new MoveUnitRequest(zone.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MovingTheNationalRootIsRejected()
    {
        using var client = await CreateOrgAdminClientAsync();
        var (otherZone, _) = await CreateZoneWithDilaAsync(client, "M12", "D21");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/units/{OrganizationSeedIds.NationalUnitId}/move", new MoveUnitRequest(otherZone.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MovingAUnitWithADuplicateCodeUnderTheNewParentFails()
    {
        using var client = await CreateOrgAdminClientAsync();
        var (_, dila) = await CreateZoneWithDilaAsync(client, "M13", "D22");
        var (otherZone, _) = await CreateZoneWithDilaAsync(client, "M14", "D22");

        var response = await client.PostAsJsonAsync($"/api/v1/units/{dila.Id}/move", new MoveUnitRequest(otherZone.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
