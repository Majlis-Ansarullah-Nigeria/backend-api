using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Identity.CurrentUser;
using HRS.Domain.Access;
using HRS.Domain.Organization;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class MeEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private async Task<(HttpClient Client, Guid UserId)> CreateLoggedInClientAsync()
    {
        var email = UniqueEmail();
        var userId = await factory.CreateUserAsync(email, "Correct-Horse-Me1");

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-Me1"));

        return (client, userId);
    }

    [Fact]
    public async Task GetMeWithNoAssignmentsReturnsAnEmptyList()
    {
        var (client, _) = await CreateLoggedInClientAsync();

        var response = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<MeProfile>();
        Assert.Empty(profile!.RoleAssignments);
        Assert.Null(profile.ActiveRoleAssignmentId);
    }

    [Fact]
    public async Task GetMeIncludesOnlyCurrentlyActiveAssignments()
    {
        var (client, userId) = await CreateLoggedInClientAsync();
        var unitId = OrganizationSeedIds.NationalUnitId;
        var now = DateTimeOffset.UtcNow;

        var activeId = await factory.CreateRoleAssignmentAsync(userId, unitId, RoleName.UnitHead, startsAt: now.AddDays(-30));
        await factory.CreateRoleAssignmentAsync(userId, unitId, RoleName.UnitHead, startsAt: now.AddDays(10)); // future
        await factory.CreateRoleAssignmentAsync(userId, unitId, RoleName.UnitHead, startsAt: now.AddDays(-60), endsAt: now.AddDays(-1)); // ended

        var response = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        var profile = await response.Content.ReadFromJsonAsync<MeProfile>();

        var assignment = Assert.Single(profile!.RoleAssignments);
        Assert.Equal(activeId, assignment.Id);
        Assert.Equal(unitId, assignment.UnitId);
        Assert.Equal(RoleName.UnitHead, assignment.Role);
    }

    [Fact]
    public async Task GetMeNamesEachAssignmentsUnitAndLevelForTheAppShell()
    {
        var (client, userId) = await CreateLoggedInClientAsync();
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var profile = await client.GetFromJsonAsync<MeProfile>("/api/v1/me");

        var assignment = Assert.Single(profile!.RoleAssignments);
        Assert.Equal(("National", UnitLevelCode.National, "National"),
            (assignment.UnitName, assignment.UnitLevel, assignment.UnitLevelName));
    }

    [Fact]
    public async Task SetActiveAssignmentToOwnActiveAssignmentSucceedsAndPersists()
    {
        var (client, userId) = await CreateLoggedInClientAsync();
        var assignmentId = await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var response = await client.PutAsJsonAsync(
            "/api/v1/me/active-assignment",
            new SetActiveAssignmentRequest(assignmentId));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var me = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        var profile = await me.Content.ReadFromJsonAsync<MeProfile>();
        Assert.Equal(assignmentId, profile!.ActiveRoleAssignmentId);
    }

    [Fact]
    public async Task SetActiveAssignmentToAnotherUsersAssignmentReturnsNotFound()
    {
        var (client, _) = await CreateLoggedInClientAsync();
        var otherUserId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Me2");
        var otherUsersAssignment = await factory.CreateRoleAssignmentAsync(otherUserId, OrganizationSeedIds.NationalUnitId, RoleName.UnitHead);

        var response = await client.PutAsJsonAsync(
            "/api/v1/me/active-assignment",
            new SetActiveAssignmentRequest(otherUsersAssignment));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetActiveAssignmentToAnExpiredAssignmentReturnsNotFound()
    {
        var (client, userId) = await CreateLoggedInClientAsync();
        var now = DateTimeOffset.UtcNow;
        var expired = await factory.CreateRoleAssignmentAsync(
            userId, OrganizationSeedIds.NationalUnitId, RoleName.UnitHead, startsAt: now.AddDays(-60), endsAt: now.AddDays(-1));

        var response = await client.PutAsJsonAsync(
            "/api/v1/me/active-assignment",
            new SetActiveAssignmentRequest(expired));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MeEndpointsRequireAuthentication()
    {
        using var client = factory.CreateAuthenticatedClient();

        var getMe = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        var setActive = await client.PutAsJsonAsync("/api/v1/me/active-assignment", new SetActiveAssignmentRequest(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, getMe.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, setActive.StatusCode);
    }
}
