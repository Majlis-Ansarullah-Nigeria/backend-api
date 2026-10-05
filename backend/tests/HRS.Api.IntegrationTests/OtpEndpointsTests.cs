using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HRS.Api.Endpoints;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public partial class OtpEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private string ExtractCode(string email)
    {
        var message = factory.EmailSender.LastSentTo(email);
        Assert.NotNull(message);

        var match = CodePattern().Match(message.TextBody);
        Assert.True(match.Success, $"No 6-digit code found in: {message.TextBody}");

        return match.Value;
    }

    [Fact]
    public async Task RequestOtpForKnownIdentifierReturnsAcceptedAndQueuesAnEmail()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-Otp-1");

        using var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new OtpRequest(email));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(factory.EmailSender.LastSentTo(email));
    }

    [Fact]
    public async Task RequestOtpForUnknownIdentifierStillReturnsAccepted()
    {
        using var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new OtpRequest(UniqueEmail()));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task RequestingACodeMoreThanThreeTimesIsRateLimited()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-Otp-2");

        using var client = factory.CreateAuthenticatedClient();

        for (var i = 0; i < 3; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new OtpRequest(email));
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        var fourth = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new OtpRequest(email));

        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
        Assert.True(fourth.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task VerifyOtpWithTheCorrectCodeSucceedsAndSetsCookies()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-Otp-3");

        using var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/otp/request", new OtpRequest(email));
        var code = ExtractCode(email);

        var response = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new OtpVerifyRequest(email, code));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(email, body!.Email);

        var me = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task VerifyOtpWithTheSameCodeTwiceFailsTheSecondTime()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-Otp-4");

        using var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/otp/request", new OtpRequest(email));
        var code = ExtractCode(email);

        var first = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new OtpVerifyRequest(email, code));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new OtpVerifyRequest(email, code));
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task VerifyOtpWithUnknownIdentifierReturnsUnauthorized()
    {
        using var client = factory.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new OtpVerifyRequest(UniqueEmail(), "123456"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FiveWrongCodesLockTheAccountJustLikePasswordLogin()
    {
        var email = UniqueEmail();
        await factory.CreateUserAsync(email, "Correct-Horse-Otp-5");

        using var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/otp/request", new OtpRequest(email));
        var correctCode = ExtractCode(email);
        var wrongCode = correctCode == "000000" ? "111111" : "000000";

        HttpStatusCode? lastStatus = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new OtpVerifyRequest(email, wrongCode));
            Assert.True(
                response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                $"Attempt {attempt + 1}: unexpected status {response.StatusCode}");
            lastStatus = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.Forbidden, lastStatus);

        // Even the correct code is rejected while locked out.
        var lockedResponse = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new OtpVerifyRequest(email, correctCode));
        Assert.Equal(HttpStatusCode.Forbidden, lockedResponse.StatusCode);
    }

    [GeneratedRegex(@"\d{6}")]
    private static partial Regex CodePattern();
}
