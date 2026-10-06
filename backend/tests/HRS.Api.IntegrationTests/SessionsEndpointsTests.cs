using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class SessionsEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    [Fact]
    public async Task GetSessionsListsOnlyOneActiveSessionWithCurrentFlagged()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-Sess1");

        using var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-Sess1"));

        var response = await client.GetAsync(new Uri("/api/v1/me/sessions", UriKind.Relative));
        var sessions = await response.Content.ReadFromJsonAsync<List<SessionSummary>>();

        var session = Assert.Single(sessions!);
        Assert.True(session.IsCurrent);
    }

    [Fact]
    public async Task LoggingInFromTwoDevicesShowsTwoSessionsWithOnlyOneCurrentPerClient()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-Sess2");

        using var deviceA = factory.CreateAuthenticatedClient();
        await deviceA.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-Sess2"));

        using var deviceB = factory.CreateAuthenticatedClient();
        await deviceB.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-Sess2"));

        var fromA = await (await deviceA.GetAsync(new Uri("/api/v1/me/sessions", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<SessionSummary>>();
        var fromB = await (await deviceB.GetAsync(new Uri("/api/v1/me/sessions", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<SessionSummary>>();

        Assert.Equal(2, fromA!.Count);
        Assert.Single(fromA, s => s.IsCurrent);
        Assert.Equal(2, fromB!.Count);
        Assert.Single(fromB, s => s.IsCurrent);

        // The "current" session differs per device, but both see the same underlying pair.
        var currentFromA = fromA.Single(s => s.IsCurrent).Id;
        var currentFromB = fromB.Single(s => s.IsCurrent).Id;
        Assert.NotEqual(currentFromA, currentFromB);
    }

    [Fact]
    public async Task RevokeOthersSignsOutEveryOtherDeviceButNotTheCallingOne()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-Sess3");

        using var deviceA = factory.CreateAuthenticatedClient();
        await deviceA.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-Sess3"));

        using var deviceB = factory.CreateAuthenticatedClient();
        await deviceB.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-Sess3"));

        var revoke = await deviceA.PostAsync(new Uri("/api/v1/me/sessions/revoke-others", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        // Device A's own refresh still works...
        var refreshA = await deviceA.PostAsync(new Uri("/api/v1/auth/refresh", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.NoContent, refreshA.StatusCode);

        // ...but device B's session was revoked, so its refresh token no longer works.
        var refreshB = await deviceB.PostAsync(new Uri("/api/v1/auth/refresh", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshB.StatusCode);
    }

    [Fact]
    public async Task RevokingASpecificOtherSessionSignsThatDeviceOutOnly()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-Sess4");

        using var deviceA = factory.CreateAuthenticatedClient();
        await deviceA.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-Sess4"));

        using var deviceB = factory.CreateAuthenticatedClient();
        await deviceB.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-Sess4"));

        var sessionsFromA = await (await deviceA.GetAsync(new Uri("/api/v1/me/sessions", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<SessionSummary>>();
        var deviceBSessionId = sessionsFromA!.Single(s => !s.IsCurrent).Id;

        var revoke = await deviceA.PostAsync(
            new Uri($"/api/v1/me/sessions/{deviceBSessionId}/revoke", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var refreshB = await deviceB.PostAsync(new Uri("/api/v1/auth/refresh", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshB.StatusCode);
    }

    [Fact]
    public async Task RevokingYourOwnCurrentSessionIsRejected()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-Sess5");

        using var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-Sess5"));

        var sessions = await (await client.GetAsync(new Uri("/api/v1/me/sessions", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<SessionSummary>>();
        var currentId = sessions!.Single().Id;

        var revoke = await client.PostAsync(new Uri($"/api/v1/me/sessions/{currentId}/revoke", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.BadRequest, revoke.StatusCode);
    }

    [Fact]
    public async Task RevokingAnotherUsersSessionReturnsNotFound()
    {
        var emailA = UniqueEmail();
        await factory.CreateUserAsync(emailA, "Correct-Horse-Sess6");
        using var clientA = factory.CreateAuthenticatedClient();
        await clientA.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(emailA, "Correct-Horse-Sess6"));

        var emailB = UniqueEmail();
        await factory.CreateUserAsync(emailB, "Correct-Horse-Sess7");
        using var clientB = factory.CreateAuthenticatedClient();
        await clientB.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(emailB, "Correct-Horse-Sess7"));

        var sessionsB = await (await clientB.GetAsync(new Uri("/api/v1/me/sessions", UriKind.Relative)))
            .Content.ReadFromJsonAsync<List<SessionSummary>>();
        var sessionBId = sessionsB!.Single().Id;

        var revoke = await clientA.PostAsync(new Uri($"/api/v1/me/sessions/{sessionBId}/revoke", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.NotFound, revoke.StatusCode);
    }
}
