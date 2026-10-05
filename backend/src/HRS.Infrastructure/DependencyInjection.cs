using System.Globalization;
using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Options;
using HRS.Infrastructure.Auth;
using HRS.Infrastructure.Common;
using HRS.Infrastructure.Email;
using HRS.Infrastructure.Identity;
using HRS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

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

        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(GetConnectionString(sp, "Redis")));

        services.AddOptions<AppOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                options.PublicUrl = configuration["App:PublicUrl"] ?? "http://localhost:3000");

        services.AddOptions<EmailOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                options.From = configuration["Email:From"]
                    ?? throw new InvalidOperationException("Missing Email:From.");
                options.SmtpHost = configuration["Email:SmtpHost"]
                    ?? throw new InvalidOperationException("Missing Email:SmtpHost.");

                var smtpPort = configuration["Email:SmtpPort"];
                if (smtpPort is not null)
                {
                    options.SmtpPort = int.Parse(smtpPort, CultureInfo.InvariantCulture);
                }
            });

        services.AddSingleton<BackgroundEmailChannel>();
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<BackgroundEmailChannel>());
        services.AddHostedService<BackgroundEmailDispatcher>();

        services.AddHealthChecks()
            .AddNpgSql(sp => GetConnectionString(sp, "Default"), name: "postgres")
            .AddRedis(sp => GetConnectionString(sp, "Redis"), name: "redis");

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // NFR-SEC-02: PBKDF2 100k+ iterations (set below via PasswordHasherOptions).
                // Policy matches the set-password mockup's criteria chips exactly (FR-IAM-002):
                // length + a digit + not a common password — no forced case/symbol mix, per
                // current NIST 800-63B guidance that length beats composition rules.
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                // FR-IAM-005: lockout after 5 failed attempts for 15 minutes.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddPasswordValidator<CommonPasswordValidator>()
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
