using HRS.Infrastructure.Persistence;
using Npgsql;

namespace HRS.Api.IntegrationTests;

/// <summary>Each process caps its Npgsql pool so the API can't exhaust Postgres's connections
/// (found by the M12.1 load test).</summary>
public class PostgresConnectionStringTests
{
    private const string Base = "Host=localhost;Port=5433;Database=hrs;Username=hrs;Password=x";

    [Fact]
    public void TheConfiguredLimitIsApplied() =>
        Assert.Equal(40, new NpgsqlConnectionStringBuilder(PostgresConnectionString.WithPoolLimit(Base, 40)).MaxPoolSize);

    [Theory]
    [InlineData("Maximum Pool Size=25")]
    [InlineData("MaxPoolSize=25")]
    [InlineData("maximum pool size=25")]
    public void ALimitWrittenIntoTheConnectionStringWins(string setting) =>
        Assert.Equal(25, new NpgsqlConnectionStringBuilder(PostgresConnectionString.WithPoolLimit($"{Base};{setting}", 40)).MaxPoolSize);

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void WithoutALimitTheStringIsUnchanged(int? limit) =>
        Assert.Equal(Base, PostgresConnectionString.WithPoolLimit(Base, limit));
}
