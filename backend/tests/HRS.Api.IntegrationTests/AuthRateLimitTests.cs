using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using HRS.Api.Endpoints;

namespace HRS.Api.IntegrationTests;

/// <summary>
/// Builds its own host rather than using the shared <see cref="ApiFactory"/> collection
/// fixture: the IP-wide "auth" rate-limit policy is shared process-wide across every request
/// a host handles, so proving it actually rejects at the configured limit needs a small
/// override that would otherwise either starve every other auth test or make this one too
/// slow to trip. Still placed in <see cref="ApiTestCollection"/> so xunit runs it
/// sequentially with the rest — two hosts built from the same Program concurrently race on
/// Serilog's static bootstrap logger ("the logger is already frozen").
/// </summary>
[Collection(ApiTestCollection.Name)]
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Disposed via xunit's IAsyncLifetime.DisposeAsync, which the analyzer doesn't recognize.")]
public sealed class AuthRateLimitTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new(authRateLimitPermits: 3);

    public Task InitializeAsync() => _factory.InitializeAsync();

    public async Task DisposeAsync() => await ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task ExceedingTheIpWideAuthLimitReturnsTooManyRequests()
    {
        using var client = _factory.CreateAuthenticatedClient();

        HttpResponseMessage? last = null;

        for (var i = 0; i < 4; i++)
        {
            last = await client.PostAsJsonAsync(
                "/api/v1/auth/otp/request",
                new OtpRequest($"{Guid.NewGuid():N}@example.org"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.True(last.Headers.Contains("Retry-After"));
    }
}
