using System.Net;
using System.Text.Json;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class HealthEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task HealthEndpointReportsPostgresAndRedisAsHealthy()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var body = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(body);

        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());

        var checks = document.RootElement
            .GetProperty("checks")
            .EnumerateArray()
            .ToDictionary(
                check => check.GetProperty("name").GetString()!,
                check => check.GetProperty("status").GetString());

        Assert.Equal("Healthy", checks["postgres"]);
        Assert.Equal("Healthy", checks["redis"]);
    }

    [Fact]
    public async Task OpenApiDocumentIsServed()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(body);

        Assert.True(document.RootElement.TryGetProperty("openapi", out _));
    }
}
