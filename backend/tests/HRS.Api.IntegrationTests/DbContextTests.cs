using HRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HRS.Api.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class DbContextTests(ApiFactory factory)
{
    [Fact]
    public async Task CanConnectAndInitialCreateMigrationIsApplied()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.True(await db.Database.CanConnectAsync());

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();

        Assert.Contains(appliedMigrations, migration => migration.Contains("InitialCreate", StringComparison.Ordinal));
    }
}
