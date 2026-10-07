using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HRS.Api.Endpoints;
using HRS.Application.Features.Identity.Invitations;
using HRS.Domain.Access;
using HRS.Domain.Organization;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public partial class InvitationsEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    // Creating a bare invitation is now org-admin gated (FR-AC-004/FR-ORG-004, M2.4) — every
    // invite is role-driven in practice. This helper is named for what these tests exercise
    // (the invite/accept flow), not for the caller's own role.
    private async Task<HttpClient> CreateLoggedInClientAsync()
    {
        var email = UniqueEmail();
        var userId = await factory.CreateUserAsync(email, "Correct-Horse-Inviter1");
        await factory.CreateRoleAssignmentAsync(userId, OrganizationSeedIds.NationalUnitId, RoleName.Secretariat);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-Inviter1"));

        return client;
    }

    private string ExtractToken(string email)
    {
        var message = factory.EmailSender.LastSentTo(email);
        Assert.NotNull(message);

        var match = TokenPattern().Match(message.TextBody);
        Assert.True(match.Success, $"No invite link found in: {message.TextBody}");

        return match.Groups[1].Value;
    }

    [Fact]
    public async Task CreateInvitationRequiresAuthentication()
    {
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/v1/invitations", new CreateInvitationRequest(UniqueEmail(), "Someone"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateInvitationRequiresOrgAdmin()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-Plain1");
        using var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-Plain1"));

        var response = await client.PostAsJsonAsync("/api/v1/invitations", new CreateInvitationRequest(UniqueEmail(), "Someone"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateInvitationForANewEmailSucceedsAndQueuesAnEmailWithTheLink()
    {
        using var client = await CreateLoggedInClientAsync();
        var inviteeEmail = UniqueEmail();

        var response = await client.PostAsJsonAsync("/api/v1/invitations", new CreateInvitationRequest(inviteeEmail, "New Head"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateInvitationResult>();
        Assert.Equal(inviteeEmail, body!.Email);

        Assert.NotNull(factory.EmailSender.LastSentTo(inviteeEmail));
        ExtractToken(inviteeEmail); // throws via Assert if the link is missing/malformed
    }

    [Fact]
    public async Task CreateInvitationForAnAlreadyRegisteredEmailReturnsConflict()
    {
        using var client = await CreateLoggedInClientAsync();
        var existingEmail = UniqueEmail();
        await factory.CreateUserAsync(existingEmail, "Correct-Horse-Existing1");

        var response = await client.PostAsJsonAsync("/api/v1/invitations", new CreateInvitationRequest(existingEmail, "Someone"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ReinvitingTheSameEmailRevokesThePriorInvitation()
    {
        using var client = await CreateLoggedInClientAsync();
        var inviteeEmail = UniqueEmail();

        await client.PostAsJsonAsync("/api/v1/invitations", new CreateInvitationRequest(inviteeEmail, "New Head"));
        var firstToken = ExtractToken(inviteeEmail);

        await client.PostAsJsonAsync("/api/v1/invitations", new CreateInvitationRequest(inviteeEmail, "New Head"));
        var secondToken = ExtractToken(inviteeEmail);

        Assert.NotEqual(firstToken, secondToken);

        using var anonymous = factory.CreateAuthenticatedClient();
        var oldPreview = await anonymous.GetAsync(new Uri($"/api/v1/invitations/{firstToken}", UriKind.Relative));
        var newPreview = await anonymous.GetAsync(new Uri($"/api/v1/invitations/{secondToken}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Conflict, oldPreview.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newPreview.StatusCode);
    }

    [Fact]
    public async Task GetInvitationForAnUnknownTokenReturnsNotFound()
    {
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync(new Uri("/api/v1/invitations/not-a-real-token", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetInvitationForAValidTokenReturnsThePreview()
    {
        using var client = await CreateLoggedInClientAsync();
        var inviteeEmail = UniqueEmail();
        await client.PostAsJsonAsync("/api/v1/invitations", new CreateInvitationRequest(inviteeEmail, "New Head"));
        var token = ExtractToken(inviteeEmail);

        using var anonymous = factory.CreateAuthenticatedClient();
        var response = await anonymous.GetAsync(new Uri($"/api/v1/invitations/{token}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<InvitationPreview>();
        Assert.Equal(inviteeEmail, preview!.Email);
        Assert.Equal("New Head", preview.Name);
    }

    [Fact]
    public async Task AcceptInvitationWithAValidTokenCreatesTheAccountAndSetsCookies()
    {
        using var client = await CreateLoggedInClientAsync();
        var inviteeEmail = UniqueEmail();
        await client.PostAsJsonAsync("/api/v1/invitations", new CreateInvitationRequest(inviteeEmail, "New Head"));
        var token = ExtractToken(inviteeEmail);

        using var inviteeClient = factory.CreateAuthenticatedClient();
        var response = await inviteeClient.PostAsJsonAsync(
            $"/api/v1/auth/invitations/{token}/accept",
            new AcceptInvitationRequest("Abdulrahman Adeyemi", "+2348030000000", "a-perfectly-fine-password1", true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(inviteeEmail, body!.Email);
        Assert.Equal("Abdulrahman Adeyemi", body.Name);

        var me = await inviteeClient.GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitationTwiceFailsTheSecondTime()
    {
        using var client = await CreateLoggedInClientAsync();
        var inviteeEmail = UniqueEmail();
        await client.PostAsJsonAsync("/api/v1/invitations", new CreateInvitationRequest(inviteeEmail, "New Head"));
        var token = ExtractToken(inviteeEmail);
        var request = new AcceptInvitationRequest("Abdulrahman Adeyemi", null, "a-perfectly-fine-password1", true);

        using var inviteeClient = factory.CreateAuthenticatedClient();
        var first = await inviteeClient.PostAsJsonAsync($"/api/v1/auth/invitations/{token}/accept", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await inviteeClient.PostAsJsonAsync($"/api/v1/auth/invitations/{token}/accept", request);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitationWithAnUnknownTokenReturnsConflict()
    {
        using var client = factory.CreateAuthenticatedClient();
        var request = new AcceptInvitationRequest("Nobody", null, "a-perfectly-fine-password1", true);

        var response = await client.PostAsJsonAsync("/api/v1/auth/invitations/not-a-real-token/accept", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitationWhenTheEmailWasRegisteredInTheMeantimeReturnsConflict()
    {
        using var client = await CreateLoggedInClientAsync();
        var inviteeEmail = UniqueEmail();
        await client.PostAsJsonAsync("/api/v1/invitations", new CreateInvitationRequest(inviteeEmail, "New Head"));
        var token = ExtractToken(inviteeEmail);

        // Someone else registers that email directly (e.g. a race with another invite path)
        // before this invitation is accepted.
        await factory.CreateUserAsync(inviteeEmail, "Correct-Horse-Race1");

        using var inviteeClient = factory.CreateAuthenticatedClient();
        var response = await inviteeClient.PostAsJsonAsync(
            $"/api/v1/auth/invitations/{token}/accept",
            new AcceptInvitationRequest("Abdulrahman Adeyemi", null, "a-perfectly-fine-password1", true));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitationWithACommonPasswordReturnsBadRequest()
    {
        using var client = await CreateLoggedInClientAsync();
        var inviteeEmail = UniqueEmail();
        await client.PostAsJsonAsync("/api/v1/invitations", new CreateInvitationRequest(inviteeEmail, "New Head"));
        var token = ExtractToken(inviteeEmail);

        using var inviteeClient = factory.CreateAuthenticatedClient();
        var response = await inviteeClient.PostAsJsonAsync(
            $"/api/v1/auth/invitations/{token}/accept",
            new AcceptInvitationRequest("Abdulrahman Adeyemi", null, "password1234", true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AcceptInvitationWithoutAgreeingToThePrivacyNoticeReturnsBadRequest()
    {
        using var client = await CreateLoggedInClientAsync();
        var inviteeEmail = UniqueEmail();
        await client.PostAsJsonAsync("/api/v1/invitations", new CreateInvitationRequest(inviteeEmail, "New Head"));
        var token = ExtractToken(inviteeEmail);

        using var inviteeClient = factory.CreateAuthenticatedClient();
        var response = await inviteeClient.PostAsJsonAsync(
            $"/api/v1/auth/invitations/{token}/accept",
            new AcceptInvitationRequest("Abdulrahman Adeyemi", null, "a-perfectly-fine-password1", false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [GeneratedRegex(@"/invite/([0-9a-fA-F]+)")]
    private static partial Regex TokenPattern();
}
