using System.Data.Common;
using Npgsql;

namespace HRS.Infrastructure.Persistence;

/// <summary>
/// Caps each process's Npgsql pool. Npgsql's default (100 per process) equals Postgres's default
/// <c>max_connections</c> (100), so under a deadline spike the API alone could take every
/// connection and lock the Worker, migrator and admins out ("sorry, too many clients already",
/// seen in the M12.1 load test). Each host sets <c>Database:MaxPoolSize</c> (API 40, Worker 15),
/// leaving room for both, migrations and an admin session. A <c>Maximum Pool Size</c> written into
/// the connection string itself still wins.
/// </summary>
public static class PostgresConnectionString
{
    public static string WithPoolLimit(string connectionString, int? maxPoolSize)
    {
        if (maxPoolSize is not { } limit || limit <= 0 || SetsPoolSize(connectionString))
        {
            return connectionString;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString) { MaxPoolSize = limit };
        return builder.ConnectionString;
    }

    /// <summary>Whether the string itself names a pool size, under any of Npgsql's spellings.
    /// (NpgsqlConnectionStringBuilder.ContainsKey only says whether a keyword exists.)</summary>
    private static bool SetsPoolSize(string connectionString)
    {
        var raw = new DbConnectionStringBuilder { ConnectionString = connectionString };
        return raw.ContainsKey("Maximum Pool Size") || raw.ContainsKey("MaxPoolSize");
    }
}
