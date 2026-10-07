using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HRS.Api.Endpoints;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public partial class PasswordResetEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private string ExtractToken(string email)
    {
        var message = factory.EmailSender.LastSentTo(email);
        Assert.NotNull(message);

        var match = ResetLinkPattern().Match(message.TextBody);
        Assert.True(match.Success, $"No reset link found in: {message.TextBody}");

        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    [Fact]
    public async Task RequestResetForKnownIdentifierReturnsAcceptedAndQueuesAnEmail()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-Reset-1");

        using var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest(email));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(factory.EmailSender.LastSentTo(email));
    }

    [Fact]
    public async Task RequestResetForUnknownIdentifierStillReturnsAccepted()
    {
        using var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest(UniqueEmail()));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task RequestingResetMoreThanThreeTimesIsRateLimited()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-Reset-2");

        using var client = factory.CreateAuthenticatedClient();

        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest(email));
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        var fourth = await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest(email));

        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
        Assert.True(fourth.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task ResetPasswordWithTheCorrectTokenSucceedsAndAllowsLoginWithTheNewPassword()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-Reset-3");

        using var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest(email));
        var token = ExtractToken(email);

        var reset = await client.PostAsJsonAsync(
            "/api/v1/auth/password/reset", new ResetPasswordRequest(email, token, "Correct-Horse-Reset-NEW1"));
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-Reset-NEW1"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task ResetPasswordWithTheSameTokenTwiceFailsTheSecondTime()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-Reset-4");

        using var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest(email));
        var token = ExtractToken(email);

        var first = await client.PostAsJsonAsync(
            "/api/v1/auth/password/reset", new ResetPasswordRequest(email, token, "Correct-Horse-Reset-NEW2"));
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await client.PostAsJsonAsync(
            "/api/v1/auth/password/reset", new ResetPasswordRequest(email, token, "Correct-Horse-Reset-NEW3"));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task ResetPasswordWithAnUnknownEmailReturnsBadRequest()
    {
        using var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/password/reset", new ResetPasswordRequest(UniqueEmail(), "garbage-token", "Correct-Horse-Reset-NEW4"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPasswordEnforcesThePasswordPolicy()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-Reset-5");

        using var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new ForgotPasswordRequest(email));
        var token = ExtractToken(email);

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/password/reset", new ResetPasswordRequest(email, token, "short1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [GeneratedRegex(@"/login/reset/([^?\s]+)\?email=")]
    private static partial Regex ResetLinkPattern();
}
