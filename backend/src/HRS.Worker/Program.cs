using System.Globalization;
using Hangfire;
using Hangfire.PostgreSql;
using HRS.Application;
using HRS.Infrastructure;
using HRS.Worker;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure();

    var connectionString = builder.Configuration["ConnectionStrings:Default"]
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Default.");

    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));
    builder.Services.AddHangfireServer();

    builder.Services.AddScoped<WindowJobsRecurringJob>();
    builder.Services.AddScoped<OutboxDispatcherRecurringJob>();

    var host = builder.Build();

    // BR-WIN-08: open/remind/overdue/close detection runs every 15 minutes; WindowJobRunner's own
    // idempotency guard (not Hangfire's) is what makes re-running safe.
    using (var scope = host.Services.CreateScope())
    {
        var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        recurringJobs.AddOrUpdate<WindowJobsRecurringJob>(
            "window-jobs",
            job => job.RunAsync(CancellationToken.None),
            "*/15 * * * *");

        // M8.1: dispatch queued notifications promptly — much tighter than the window jobs'
        // 15-minute cadence, since these fire right after a user action (acknowledge, comment).
        recurringJobs.AddOrUpdate<OutboxDispatcherRecurringJob>(
            "outbox-dispatcher",
            job => job.RunAsync(CancellationToken.None),
            "* * * * *");
    }

    host.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "HRS.Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
