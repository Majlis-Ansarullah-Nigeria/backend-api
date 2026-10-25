using System.Globalization;
using System.Text;
using Npgsql;

namespace HRS.LoadTestSeeder;

/// <summary>Accepts only connection strings whose host is this machine.</summary>
public static class LocalDatabaseGuard
{
    public static bool IsLocal(string connectionString)
    {
        var host = new NpgsqlConnectionStringBuilder(connectionString).Host ?? "";
        return host.Split(',').All(h => h.Trim() is "localhost" or "127.0.0.1" or "::1");
    }
}

/// <summary>The k6 script's input: one row per head, plus the window they all submit to.</summary>
public static class LoadTestCsv
{
    public static string Heads(LoadTestSeedResult result)
    {
        var builder = new StringBuilder("email,unitId,unitCode,level,windowId\n");
        foreach (var head in result.Heads)
        {
            builder.Append(CultureInfo.InvariantCulture, $"{head.Email},{head.UnitId},{head.UnitCode},{head.Level},{result.WindowId}\n");
        }

        return builder.ToString();
    }
}
