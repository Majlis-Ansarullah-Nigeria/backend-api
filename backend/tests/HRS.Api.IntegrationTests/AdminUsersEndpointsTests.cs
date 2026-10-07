using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Admin.Users;
using HRS.Application.Features.Organization.Units;
using HRS.Domain.Access;
using HRS.Domain.Organization;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class AdminUsersEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private async Task<(HttpClient Client, Guid UserId)> CreateOrgAdminClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Adm1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, userId);
    }

    private async Task<HttpClient> CreatePlainUserClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Adm2";
        await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private static async Task<UnitSummary> CreateZoneAsync(HttpClient client, string code)
    {
        var request = new CreateUnitRequest(code, $"Zone {code}", OrganizationSeedIds.ZoneLevelId, OrganizationSeedIds.NationalUnitId, null, null, null, null, null);
        var response = await client.PostAsJsonAsync("/api/v1/units", request);
        return (await response.Content.ReadFromJsonAsync<UnitSummary>())!;
    }

    [Fact]
    public async Task GetUsersRequiresOrgAdmin()
    {
        using var client = await CreatePlainUserClientAsync();

        var response = await client.GetAsync(new Uri("/api/v1/admin/users", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsersShowsARealUsersRoleAssignmentWithTheUnitName()
    {
        var (admin, _) = await CreateOrgAdminClientAsync();
        using var client = admin;
        var zone = await CreateZoneAsync(client, "AU1");

        var headEmail = UniqueEmail();
        var headId = await factory.CreateUserAsync(headEmail, "Correct-Horse-Head4", name: "Zone Head");
        await factory.CreateRoleAssignmentAsync(headId, zone.Id, RoleName.UnitHead);

        var users = await (await client.GetAsync(new Uri("/api/v1/admin/users", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<UserListItem>>();

        var user = users!.Single(u => u.UserId == headId);
        Assert.Equal(UserAccountStatus.Active, user.Status);
        var assignment = Assert.Single(user.RoleAssignments);
        Assert.Equal("Zone AU1", assignment.UnitName);
        Assert.True(assignment.IsActive);
    }

    [Fact]
    public async Task GetUsersShowsAPendingInvitationAsInvited()
    {
        var (admin, _) = await CreateOrgAdminClientAsync();
        using var client = admin;
        var zone = await CreateZoneAsync(client, "AU2");
        var inviteeEmail = UniqueEmail();

        await client.PostAsJsonAsync(
            $"/api/v1/units/{zone.Id}/assignments",
            new CreateRoleAssignmentRequest(inviteeEmail, "Future Head", RoleName.UnitHead, null, DateTimeOffset.UtcNow, null));

        var users = await (await client.GetAsync(new Uri("/api/v1/admin/users", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<UserListItem>>();

        var invited = users!.Single(u => u.Email == inviteeEmail);
        Assert.Equal(UserAccountStatus.Invited, invited.Status);
        Assert.Null(invited.UserId);
        Assert.NotNull(invited.InvitationId);
    }

    [Fact]
    public async Task DeactivatingAUserBlocksPasswordLogin()
    {
        var (admin, _) = await CreateOrgAdminClientAsync();
        using var client = admin;
        var targetEmail = UniqueEmail();
        var targetId = await factory.CreateUserAsync(targetEmail, "Correct-Horse-Target1");

        var response = await client.PostAsync(new Uri($"/api/v1/admin/users/{targetId}/deactivate", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var targetClient = factory.CreateAuthenticatedClient();
        var login = await targetClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(targetEmail, "Correct-Horse-Target1"));

        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task AnAdminCannotDeactivateTheirOwnAccount()
    {
        var (client, adminId) = await CreateOrgAdminClientAsync();
        using var admin = client;

        var response = await admin.PostAsync(new Uri($"/api/v1/admin/users/{adminId}/deactivate", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReactivatingAUserRestoresLogin()
    {
        var (admin, _) = await CreateOrgAdminClientAsync();
        using var client = admin;
        var targetEmail = UniqueEmail();
        var targetId = await factory.CreateUserAsync(targetEmail, "Correct-Horse-Target2");
        await client.PostAsync(new Uri($"/api/v1/admin/users/{targetId}/deactivate", UriKind.Relative), null);

        var reactivate = await client.PostAsync(new Uri($"/api/v1/admin/users/{targetId}/reactivate", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.NoContent, reactivate.StatusCode);

        using var targetClient = factory.CreateAuthenticatedClient();
        var login = await targetClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(targetEmail, "Correct-Horse-Target2"));

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task AdminResetMfaClearsTotpEvenWhenMandatoryForTheUsersRole()
    {
        var (admin, _) = await CreateOrgAdminClientAsync();
        using var client = admin;

        // The target is itself a Secretariat (MFA mandatory) — self-service disable would be
        // blocked (M1.5's DisableTotpCommandHandler), but the admin reset isn't.
        var targetEmail = UniqueEmail();
        var targetId = await factory.CreateUserAsync(targetEmail, "Correct-Horse-Target3");
        await factory.CreateRoleAssignmentAsync(targetId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        using var targetClient = factory.CreateAuthenticatedClient();
        await targetClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(targetEmail, "Correct-Horse-Target3"));
        var setup = await targetClient.PostAsync(new Uri("/api/v1/me/mfa/totp/setup", UriKind.Relative), null);
        var enrollment = await setup.Content.ReadFromJsonAsync<HRS.Application.Common.Interfaces.TotpEnrollment>();
        var code = TotpTestHelper.GenerateCode(enrollment!.SharedKey);
        await targetClient.PostAsJsonAsync("/api/v1/me/mfa/totp/confirm", new ConfirmTotpEnrollmentRequest(code));

        var reset = await client.PostAsync(new Uri($"/api/v1/admin/users/{targetId}/reset-mfa", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
    }

    [Fact]
    public async Task RevokeAllSessionsEndsTheUsersActiveSession()
    {
        var (admin, _) = await CreateOrgAdminClientAsync();
        using var client = admin;
        var targetEmail = UniqueEmail();
        var targetId = await factory.CreateUserAsync(targetEmail, "Correct-Horse-Target4");

        using var targetClient = factory.CreateAuthenticatedClient();
        await targetClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(targetEmail, "Correct-Horse-Target4"));
        var meBefore = await targetClient.GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, meBefore.StatusCode);

        var revoke = await client.PostAsync(new Uri($"/api/v1/admin/users/{targetId}/revoke-sessions", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var refresh = await targetClient.PostAsync(new Uri("/api/v1/auth/refresh", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
