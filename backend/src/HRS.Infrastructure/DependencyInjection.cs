using HRS.Application.Common.Interfaces;
using HRS.Infrastructure.Auth;
using HRS.Infrastructure.Common;
using HRS.Infrastructure.Identity;
using HRS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();

        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(GetConnectionString(sp, "Default"))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddHealthChecks()
            .AddNpgSql(sp => GetConnectionString(sp, "Default"), name: "postgres")
            .AddRedis(sp => GetConnectionString(sp, "Redis"), name: "redis");

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // NFR-SEC-02: PBKDF2 100k+ iterations (set below via PasswordHasherOptions).
                options.Password.RequiredLength = 10;

                // FR-IAM-005: lockout after 5 failed attempts for 15 minutes.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddSignInManager();

        services.Configure<PasswordHasherOptions>(options => options.IterationCount = 100_000);

        services.AddOptions<AuthOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                options.SigningKey = configuration["Auth:SigningKey"]
                    ?? throw new InvalidOperationException("Missing Auth:SigningKey."));

        services.AddScoped<IAccessTokenGenerator, JwtAccessTokenGenerator>();
        services.AddScoped<IIdentityService, IdentityService>();

        return services;
    }

    private static string GetConnectionString(IServiceProvider services, string name) =>
        services.GetRequiredService<IConfiguration>()[$"ConnectionStrings:{name}"]
            ?? throw new InvalidOperationException($"Missing ConnectionStrings:{name}.");
}
