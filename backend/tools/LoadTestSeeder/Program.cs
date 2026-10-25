// M12.1 dev tool: seeds (or removes) the throwaway organization the k6 deadline-spike test runs
// against (NFR-PRF-03). Never part of a deploy, and it refuses any database that isn't on this
// machine. See perf/README.md.
//
//   dotnet run --project backend/tools/LoadTestSeeder -- seed [--units 5000] [--out perf/k6/data/heads.csv]
//   dotnet run --project backend/tools/LoadTestSeeder -- cleanup
//
// Options: --connection <npgsql connection string> (default: the docker-compose dev database),
// --password <shared password> (default: LoadTest-Password-2026).
using System.Globalization;
using System.Text;
using HRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRS.LoadTestSeeder;

internal static class Program
{
    private const string DefaultConnection = "Host=localhost;Port=5433;Database=hrs;Username=hrs;Password=hrs_dev_password";
    private const string DefaultPassword = "LoadTest-Password-2026";

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is not ("seed" or "cleanup"))
        {
            Console.WriteLine("Usage: LoadTestSeeder seed [--units 5000] [--out perf/k6/data/heads.csv] [--connection ...] [--password ...]");
            Console.WriteLine("       LoadTestSeeder cleanup [--connection ...]");
            return 1;
        }

        var options = ParseOptions(args.Skip(1).ToArray());
        var connectionString = options.GetValueOrDefault("connection", DefaultConnection);
        if (!LocalDatabaseGuard.IsLocal(connectionString))
        {
            Console.Error.WriteLine("Refusing to run: the load-test seeder only works against a database on localhost.");
            return 2;
        }

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new AppDbContext(dbOptions);

        if (args[0] == "cleanup")
        {
            await LoadTestOrganization.CleanupAsync(db);
            Console.WriteLine("Removed the load-test organization, users, report type and windows.");
            return 0;
        }

        var units = int.Parse(options.GetValueOrDefault("units", "5000"), CultureInfo.InvariantCulture);
        var output = options.GetValueOrDefault("out", Path.Combine("perf", "k6", "data", "heads.csv"));
        var password = options.GetValueOrDefault("password", DefaultPassword);
        var shape = units == LoadTestShape.Default.TotalUnits
            ? LoadTestShape.Default
            : new LoadTestShape(Math.Clamp(units / 500, 1, 10), Math.Clamp(units / 500, 1, 10), units);

        var started = DateTimeOffset.UtcNow;
        var result = await LoadTestOrganization.SeedAsync(db, shape, password, DateTimeOffset.UtcNow);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllTextAsync(output, LoadTestCsv.Heads(result), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Console.WriteLine($"Seeded {result.Heads.Count} units and heads in {(DateTimeOffset.UtcNow - started).TotalSeconds:0.0} s.");
        Console.WriteLine($"Window {result.WindowId} is open (due in 2 hours). Secretariat: {result.SecretariatEmail}");
        Console.WriteLine($"Heads written to {Path.GetFullPath(output)}. Shared password: {password}");
        return 0;
    }

    private static Dictionary<string, string> ParseOptions(string[] values)
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i + 1 < values.Length; i += 2)
        {
            parsed[values[i].TrimStart('-')] = values[i + 1];
        }

        return parsed;
    }
}
