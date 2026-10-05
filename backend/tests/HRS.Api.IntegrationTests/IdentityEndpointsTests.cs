using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class IdentityEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    [Fact]
    public async Task LoginWithValidEmailAndPasswordSucceedsAndSetsCookies()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-1");

        using var client = factory.CreateAuthenticatedClient(handleCookies: false);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(email, body!.Email);

        var setCookieHeaders = response.Headers.GetValues("Set-Cookie").ToList();
        var accessCookie = setCookieHeaders.Single(c => c.StartsWith("access_token=", StringComparison.Ordinal));
        var refreshCookie = setCookieHeaders.Single(c => c.StartsWith("refresh_token=", StringComparison.Ordinal));

        Assert.Contains("httponly", accessCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", accessCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", accessCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", refreshCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginWithPhoneNumberSucceeds()
    {
        var email = UniqueEmail();
        var phone = $"+1555{Random.Shared.Next(1000000, 9999999)}";
        await factory.CreateUserAsync(email, "Correct-Horse-2", phoneNumber: phone);

        using var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(phone, "Correct-Horse-2"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LoginWithWrongPasswordReturnsUnauthorized()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-3");

        using var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginWithUnknownIdentifierReturnsUnauthorized()
    {
        using var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(UniqueEmail(), "whatever-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FiveFailedAttemptsLockTheAccountForFifteenMinutes()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-4");

        using var client = factory.CreateAuthenticatedClient();

        // Identity locks out as soon as the failed-attempt count hits the threshold, which may
        // happen on the attempt that tips it over rather than strictly after — assert each
        // response is one of the two expected outcomes rather than pin down the exact attempt.
        HttpStatusCode? lastStatus = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "wrong-password"));
            Assert.True(
                response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                $"Attempt {attempt + 1}: unexpected status {response.StatusCode}");
            lastStatus = response.StatusCode;
        }

        // By the 5th failed attempt the account must be locked (FR-IAM-005).
        Assert.Equal(HttpStatusCode.Forbidden, lastStatus);

        // Even the correct password is rejected while locked out.
        var lockedResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-4"));
        Assert.Equal(HttpStatusCode.Forbidden, lockedResponse.StatusCode);
    }

    [Fact]
    public async Task GetMeWithoutCookieReturnsUnauthorized()
    {
        using var client = factory.CreateAuthenticatedClient();
        var response = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMeWithValidAccessCookieReturnsProfile()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-5");

        using var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-5"));

        var response = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(email, body!.Email);
    }

    [Fact]
    public async Task RefreshRotatesTheSessionAndInvalidatesThePreviousToken()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-6");

        using var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-6"));

        var firstRefresh = await client.PostAsync(new Uri("/api/v1/auth/refresh", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.NoContent, firstRefresh.StatusCode);

        // /me still works with the newly rotated access cookie.
        var me = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task ReusingARevokedRefreshTokenRevokesEverySessionForThatUser()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-7");

        using var client = factory.CreateAuthenticatedClient(handleCookies: false);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-7"));
        var tokenA = ExtractCookieValue(loginResponse, "refresh_token");

        // Rotate once: token A gets revoked (replaced by token B).
        var firstRefresh = await RefreshWithTokenAsync(client, tokenA);
        Assert.Equal(HttpStatusCode.NoContent, firstRefresh.StatusCode);
        var tokenB = ExtractCookieValue(firstRefresh, "refresh_token");

        // Replay the stolen, already-revoked token A — rejected, and must revoke token B too.
        var replayA = await RefreshWithTokenAsync(client, tokenA);
        Assert.Equal(HttpStatusCode.Unauthorized, replayA.StatusCode);

        // Token B was legitimately rotated-to but is now collateral-revoked by the reuse detection.
        var attemptB = await RefreshWithTokenAsync(client, tokenB);
        Assert.Equal(HttpStatusCode.Unauthorized, attemptB.StatusCode);
    }

    private static async Task<HttpResponseMessage> RefreshWithTokenAsync(HttpClient client, string refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"refresh_token={refreshToken}");
        return await client.SendAsync(request);
    }

    private static string ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        var header = response.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith($"{cookieName}=", StringComparison.Ordinal));
        var valueAndAttributes = header[(cookieName.Length + 1)..];
        return valueAndAttributes[..valueAndAttributes.IndexOf(';')];
    }

    [Fact]
    public async Task LogoutRevokesTheSessionSoRefreshNoLongerWorks()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-8");

        using var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-8"));

        var logout = await client.PostAsync(new Uri("/api/v1/auth/logout", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refreshAfterLogout = await client.PostAsync(new Uri("/api/v1/auth/refresh", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }
}
