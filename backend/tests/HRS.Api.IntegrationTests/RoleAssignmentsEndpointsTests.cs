using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Features.Organization.RoleAssignments;
using HRS.Application.Features.Organization.Units;
using HRS.Domain.Access;
using HRS.Domain.Organization;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class RoleAssignmentsEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private async Task<HttpClient> CreateOrgAdminClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Role1";
        var userId = await factory.CreateUserAsync(email, password);
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return client;
    }

    private async Task<HttpClient> CreatePlainUserClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Role2";
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
    public async Task CreatingAnAssignmentRequiresOrgAdmin()
    {
        using var client = await CreatePlainUserClientAsync();
        var admin = await CreateOrgAdminClientAsync();
        var zone = await CreateZoneAsync(admin, "RA1");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/units/{zone.Id}/assignments",
            new CreateRoleAssignmentRequest(UniqueEmail(), "Someone", RoleName.UnitHead, null, DateTimeOffset.UtcNow, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssigningAHeadToAnExistingUserCreatesTheAssignmentImmediately()
    {
        using var admin = await CreateOrgAdminClientAsync();
        var zone = await CreateZoneAsync(admin, "RA2");
        var headEmail = UniqueEmail();
        await factory.CreateUserAsync(headEmail, "Correct-Horse-Head1", name: "New Head");

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/units/{zone.Id}/assignments",
            new CreateRoleAssignmentRequest(headEmail, "New Head", RoleName.UnitHead, null, DateTimeOffset.UtcNow, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateRoleAssignmentResult>();
        Assert.False(result!.Invited);
        Assert.NotNull(result.RoleAssignmentId);
    }

    [Fact]
    public async Task AssigningAHeadToAnUnregisteredEmailInvitesThem()
    {
        using var admin = await CreateOrgAdminClientAsync();
        var zone = await CreateZoneAsync(admin, "RA3");
        var headEmail = UniqueEmail();

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/units/{zone.Id}/assignments",
            new CreateRoleAssignmentRequest(headEmail, "Future Head", RoleName.UnitHead, null, DateTimeOffset.UtcNow, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateRoleAssignmentResult>();
        Assert.True(result!.Invited);
        Assert.Null(result.RoleAssignmentId);
        Assert.NotNull(factory.EmailSender.LastSentTo(headEmail));
    }

    [Fact]
    public async Task AssigningANewHeadEndsTheUnitsCurrentActiveHead()
    {
        using var admin = await CreateOrgAdminClientAsync();
        var zone = await CreateZoneAsync(admin, "RA4");

        var firstHeadId = await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Head2", name: "First Head");
        await factory.CreateRoleAssignmentAsync(firstHeadId, zone.Id, RoleName.UnitHead);

        var secondHeadEmail = UniqueEmail();
        await factory.CreateUserAsync(secondHeadEmail, "Correct-Horse-Head3", name: "Second Head");

        await admin.PostAsJsonAsync(
            $"/api/v1/units/{zone.Id}/assignments",
            new CreateRoleAssignmentRequest(secondHeadEmail, "Second Head", RoleName.UnitHead, null, DateTimeOffset.UtcNow, null));

        var tree = await (await admin.GetAsync(new Uri("/api/v1/units/tree", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<HRS.Application.Features.Organization.Units.UnitTreeNode>>();
        var node = tree!.Single(n => n.Id == zone.Id);
        Assert.Equal("Second Head", node.HeadName);
    }

    [Fact]
    public async Task DelegateAssignmentRequiresAScope()
    {
        using var admin = await CreateOrgAdminClientAsync();
        var zone = await CreateZoneAsync(admin, "RA5");

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/units/{zone.Id}/assignments",
            new CreateRoleAssignmentRequest(UniqueEmail(), "Delegate 1", RoleName.Delegate, null, DateTimeOffset.UtcNow, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AThirdActiveDelegateIsRejected()
    {
        using var admin = await CreateOrgAdminClientAsync();
        var zone = await CreateZoneAsync(admin, "RA6");

        await factory.CreateRoleAssignmentAsync(
            await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Del1"), zone.Id, RoleName.Delegate, DelegateScope.DraftOnly);
        await factory.CreateRoleAssignmentAsync(
            await factory.CreateUserAsync(UniqueEmail(), "Correct-Horse-Del2"), zone.Id, RoleName.Delegate, DelegateScope.DraftAndSubmit);

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/units/{zone.Id}/assignments",
            new CreateRoleAssignmentRequest(UniqueEmail(), "Delegate 3", RoleName.Delegate, DelegateScope.DraftOnly, DateTimeOffset.UtcNow, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AssigningToAnArchivedUnitIsRejected()
    {
        using var admin = await CreateOrgAdminClientAsync();
        var zone = await CreateZoneAsync(admin, "RA7");
        await admin.DeleteAsync(new Uri($"/api/v1/units/{zone.Id}", UriKind.Relative));

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/units/{zone.Id}/assignments",
            new CreateRoleAssignmentRequest(UniqueEmail(), "Someone", RoleName.UnitHead, null, DateTimeOffset.UtcNow, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task InvitingAHeadWithTermDatesHonorsThemOnAccept()
    {
        using var admin = await CreateOrgAdminClientAsync();
        var zone = await CreateZoneAsync(admin, "RA8");
        var headEmail = UniqueEmail();
        // Already active (not a future start) — GetMeQuery only returns currently-active
        // assignments, confirmed by MeEndpointsTests.GetMeIncludesOnlyCurrentlyActiveAssignments.
        var startsAt = DateTimeOffset.UtcNow.AddDays(-1);
        var endsAt = DateTimeOffset.UtcNow.AddYears(1);

        await admin.PostAsJsonAsync(
            $"/api/v1/units/{zone.Id}/assignments",
            new CreateRoleAssignmentRequest(headEmail, "Future Head", RoleName.UnitHead, null, startsAt, endsAt));

        var inviteUrl = ExtractInviteUrl(headEmail);
        var token = inviteUrl.Split('/').Last();

        using var inviteClient = factory.CreateAuthenticatedClient();
        var accept = await inviteClient.PostAsJsonAsync(
            $"/api/v1/auth/invitations/{token}/accept",
            new { name = "Future Head", phone = (string?)null, password = "Correct-Horse-FutureHead1", agreedToPrivacyNotice = true });

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var me = await inviteClient.GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        var profile = await me.Content.ReadFromJsonAsync<HRS.Application.Features.Identity.CurrentUser.MeProfile>();
        var assignment = Assert.Single(profile!.RoleAssignments);
        Assert.Equal(startsAt.ToUnixTimeSeconds(), assignment.StartsAt.ToUnixTimeSeconds());
        Assert.Equal(endsAt.ToUnixTimeSeconds(), assignment.EndsAt!.Value.ToUnixTimeSeconds());
    }

    private string ExtractInviteUrl(string email)
    {
        var message = factory.EmailSender.LastSentTo(email);
        Assert.NotNull(message);
        var match = System.Text.RegularExpressions.Regex.Match(message.TextBody, @"https?://\S+/invite/\S+");
        Assert.True(match.Success, $"No invite link found in: {message.TextBody}");
        return match.Value;
    }
}
