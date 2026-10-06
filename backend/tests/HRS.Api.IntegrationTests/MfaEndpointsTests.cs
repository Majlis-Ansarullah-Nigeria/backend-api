using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Identity.CurrentUser;
using HRS.Domain.Access;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class MfaEndpointsTests(ApiFactory factory)
{
    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private async Task<(HttpClient Client, string Email, string Password)> CreateLoggedInClientAsync()
    {
        var email = UniqueEmail();
        const string password = "Correct-Horse-Mfa1";
        await factory.CreateUserAsync(email, password);

        var client = factory.CreateAuthenticatedClient();
        await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        return (client, email, password);
    }

    /// <summary>Enrolls TOTP and returns (the raw shared key, the recovery codes) for further use.</summary>
    private static async Task<(string SharedKey, IReadOnlyList<string> RecoveryCodes)> EnrollTotpAsync(HttpClient client)
    {
        var setup = await client.PostAsync(new Uri("/api/v1/me/mfa/totp/setup", UriKind.Relative), null);
        var enrollment = await setup.Content.ReadFromJsonAsync<TotpEnrollment>();

        var code = TotpTestHelper.GenerateCode(enrollment!.SharedKey);
        var confirm = await client.PostAsJsonAsync("/api/v1/me/mfa/totp/confirm", new ConfirmTotpEnrollmentRequest(code));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var body = await confirm.Content.ReadFromJsonAsync<ConfirmTotpEnrollmentResponse>();
        return (enrollment.SharedKey, body!.RecoveryCodes);
    }

    [Fact]
    public async Task ConfirmingEnrollmentReturnsTenRecoveryCodesAndEnablesMfa()
    {
        var (client, email, password) = await CreateLoggedInClientAsync();

        var (_, recoveryCodes) = await EnrollTotpAsync(client);
        Assert.Equal(10, recoveryCodes.Count);

        using var freshClient = factory.CreateAuthenticatedClient(handleCookies: false);
        var login = await freshClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.Accepted, login.StatusCode);
        Assert.False(login.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task ConfirmingEnrollmentWithAWrongCodeFails()
    {
        var (client, _, _) = await CreateLoggedInClientAsync();
        await client.PostAsync(new Uri("/api/v1/me/mfa/totp/setup", UriKind.Relative), null);

        var confirm = await client.PostAsJsonAsync("/api/v1/me/mfa/totp/confirm", new ConfirmTotpEnrollmentRequest("000000"));

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
    }

    [Fact]
    public async Task EnrollConfirmLoginVerifyFullRoundTrip()
    {
        var (client, email, password) = await CreateLoggedInClientAsync();
        var (sharedKey, _) = await EnrollTotpAsync(client);

        using var loginClient = factory.CreateAuthenticatedClient();
        var login = await loginClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.Accepted, login.StatusCode);
        var challenge = await login.Content.ReadFromJsonAsync<MfaChallengeResponse>();

        var loginCode = TotpTestHelper.GenerateCode(sharedKey);
        var verify = await loginClient.PostAsJsonAsync(
            "/api/v1/auth/mfa/verify",
            new MfaVerifyRequest(challenge!.ChallengeToken, loginCode));

        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var me = await loginClient.GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task VerifyingMfaWithAWrongCodeIsRejected()
    {
        var (client, email, password) = await CreateLoggedInClientAsync();
        await EnrollTotpAsync(client);

        using var loginClient = factory.CreateAuthenticatedClient();
        var login = await loginClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        var challenge = await login.Content.ReadFromJsonAsync<MfaChallengeResponse>();

        var verify = await loginClient.PostAsJsonAsync(
            "/api/v1/auth/mfa/verify",
            new MfaVerifyRequest(challenge!.ChallengeToken, "000000"));

        Assert.Equal(HttpStatusCode.Unauthorized, verify.StatusCode);
    }

    [Fact]
    public async Task LoggingInWithARecoveryCodeWorksOnceThenIsConsumed()
    {
        var (client, email, password) = await CreateLoggedInClientAsync();
        var (_, recoveryCodes) = await EnrollTotpAsync(client);
        var recoveryCode = recoveryCodes[0];

        using var firstAttempt = factory.CreateAuthenticatedClient();
        var firstLogin = await firstAttempt.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        var firstChallenge = await firstLogin.Content.ReadFromJsonAsync<MfaChallengeResponse>();

        var firstVerify = await firstAttempt.PostAsJsonAsync(
            "/api/v1/auth/mfa/verify",
            new MfaVerifyRequest(firstChallenge!.ChallengeToken, recoveryCode));
        Assert.Equal(HttpStatusCode.OK, firstVerify.StatusCode);

        using var secondAttempt = factory.CreateAuthenticatedClient();
        var secondLogin = await secondAttempt.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        var secondChallenge = await secondLogin.Content.ReadFromJsonAsync<MfaChallengeResponse>();

        var secondVerify = await secondAttempt.PostAsJsonAsync(
            "/api/v1/auth/mfa/verify",
            new MfaVerifyRequest(secondChallenge!.ChallengeToken, recoveryCode));
        Assert.Equal(HttpStatusCode.Unauthorized, secondVerify.StatusCode);
    }

    [Fact]
    public async Task FiveWrongMfaCodesLockTheAccount()
    {
        var (client, email, password) = await CreateLoggedInClientAsync();
        await EnrollTotpAsync(client);

        using var loginClient = factory.CreateAuthenticatedClient();
        var login = await loginClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        var challenge = await login.Content.ReadFromJsonAsync<MfaChallengeResponse>();

        HttpStatusCode? lastStatus = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await loginClient.PostAsJsonAsync(
                "/api/v1/auth/mfa/verify",
                new MfaVerifyRequest(challenge!.ChallengeToken, "000000"));
            Assert.True(
                response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                $"Attempt {attempt + 1}: unexpected status {response.StatusCode}");
            lastStatus = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.Forbidden, lastStatus);
    }

    [Fact]
    public async Task DisablingTotpWorksForAUserWithoutAMandatoryRole()
    {
        var (client, email, password) = await CreateLoggedInClientAsync();
        await EnrollTotpAsync(client);

        var disable = await client.PostAsync(new Uri("/api/v1/me/mfa/totp/disable", UriKind.Relative), null);
        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);

        using var loginClient = factory.CreateAuthenticatedClient(handleCookies: false);
        var login = await loginClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task DisablingTotpIsRejectedForAnActiveSecretariatAssignment()
    {
        var (client, _, _) = await CreateLoggedInClientAsync();
        var meResponse = await client.GetAsync(new Uri("/api/v1/me", UriKind.Relative));
        var me = await meResponse.Content.ReadFromJsonAsync<MeProfile>();

        await EnrollTotpAsync(client);
        await factory.CreateRoleAssignmentAsync(me!.Id, Guid.NewGuid(), RoleName.Secretariat);

        var disable = await client.PostAsync(new Uri("/api/v1/me/mfa/totp/disable", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.Forbidden, disable.StatusCode);
    }
}
