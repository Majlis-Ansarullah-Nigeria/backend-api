using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Infrastructure.Identity;
using HRS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace HRS.Api.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly int? _authRateLimitPermits;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("hrs")
        .WithUsername("hrs")
        .WithPassword("hrs_test_password")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    public CapturingEmailSender EmailSender { get; } = new();

    // xunit requires exactly one public constructor on a collection fixture, so the
    // rate-limit override (used only by the standalone AuthRateLimitTests) is internal.
    public ApiFactory()
        : this(authRateLimitPermits: null)
    {
    }

    internal ApiFactory(int? authRateLimitPermits)
    {
        _authRateLimitPermits = authRateLimitPermits;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var settings = new List<KeyValuePair<string, string?>>
            {
                new("ConnectionStrings:Default", _postgres.GetConnectionString()),
                new("ConnectionStrings:Redis", _redis.GetConnectionString()),
            };

            // Defaults to the production-sized limit, which is generous enough not to
            // interfere with the rest of the suite sharing this host; a dedicated test
            // overrides it on its own isolated factory instance (AuthRateLimitTests).
            if (_authRateLimitPermits is { } permits)
            {
                settings.Add(new KeyValuePair<string, string?>(
                    "RateLimiting:Auth:PermitLimit", permits.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }

            configBuilder.AddInMemoryCollection(settings);
        });

        // Replaces the registration from AddInfrastructure() — no live SMTP server in tests.
        builder.ConfigureTestServices(services => services.AddSingleton<IEmailSender>(EmailSender));
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

    /// <summary>Seeds a user directly via UserManager, bypassing the invitation accept flow.</summary>
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

    /// <summary>
    /// Seeds a role assignment directly — there's no HTTP endpoint for this yet
    /// (assigning a head is M2.4's POST /units/{id}/assignments).
    /// </summary>
    public async Task<Guid> CreateRoleAssignmentAsync(
        Guid userId,
        Guid unitId,
        RoleName role,
        DelegateScope? delegateScope = null,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var assignment = new RoleAssignment(
            Guid.CreateVersion7(),
            userId,
            unitId,
            role,
            delegateScope,
            startsAt ?? clock.UtcNow,
            endsAt,
            appointedBy: null);

        db.RoleAssignments.Add(assignment);
        await db.SaveChangesAsync();

        return assignment.Id;
    }
}
