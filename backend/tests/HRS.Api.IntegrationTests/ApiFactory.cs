using HRS.Infrastructure.Identity;
using HRS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace HRS.Api.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("hrs")
        .WithUsername("hrs")
        .WithPassword("hrs_test_password")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) => configBuilder.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>("ConnectionStrings:Default", _postgres.GetConnectionString()),
            new KeyValuePair<string, string?>("ConnectionStrings:Redis", _redis.GetConnectionString()),
        ]));
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// A client that actually round-trips cookies on subsequent requests. Our auth cookies are
    /// marked Secure, and CookieContainer (correctly) withholds Secure cookies from plain-http
    /// requests — TestServer's default base address is http://localhost. Pointing the client at
    /// an https:// base address satisfies that check without any real TLS being involved.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(bool handleCookies = true) =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = handleCookies,
        });

    /// <summary>Seeds a user directly via UserManager — no registration endpoint exists yet (M1.3).</summary>
    public async Task<Guid> CreateUserAsync(string email, string password, string? phoneNumber = null, string name = "Test User")
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            UserName = email,
            Email = email,
            PhoneNumber = phoneNumber,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return user.Id;
    }
}
