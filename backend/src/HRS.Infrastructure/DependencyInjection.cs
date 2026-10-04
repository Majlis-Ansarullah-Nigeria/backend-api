using HRS.Application.Common.Interfaces;
using HRS.Infrastructure.Common;
using HRS.Infrastructure.Persistence;
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

        return services;
    }

    private static string GetConnectionString(IServiceProvider services, string name) =>
        services.GetRequiredService<IConfiguration>()[$"ConnectionStrings:{name}"]
            ?? throw new InvalidOperationException($"Missing ConnectionStrings:{name}.");
}
