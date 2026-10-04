using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HRS.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` build an <see cref="AppDbContext"/> without starting the full host.
/// Only used by the CLI tooling; never shipped as part of the running API or Worker.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=hrs;Username=hrs;Password=hrs_dev_password";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? DesignTimeConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();

        return new AppDbContext(optionsBuilder.Options);
    }
}
