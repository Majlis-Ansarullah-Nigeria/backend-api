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
