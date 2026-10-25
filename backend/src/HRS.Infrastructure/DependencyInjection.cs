using System.Globalization;
using Amazon.S3;
using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Options;
using HRS.Application.Features.Notifications.Push;
using HRS.Infrastructure.Access;
using HRS.Infrastructure.Auth;
using HRS.Infrastructure.Common;
using HRS.Infrastructure.Compiled;
using HRS.Infrastructure.Email;
using HRS.Infrastructure.Excel;
using HRS.Infrastructure.Files;
using HRS.Infrastructure.Identity;
using HRS.Infrastructure.Jobs;
using HRS.Infrastructure.Notifications;
using HRS.Infrastructure.Organization;
using HRS.Infrastructure.Pdf;
using HRS.Infrastructure.Persistence;
using HRS.Infrastructure.Webhooks;
using HRS.Infrastructure.Security;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
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

        // FR-NOT-001: the live badge. Api and Worker each register this identically — Api maps
        // the hub endpoint clients connect to, Worker only ever publishes through the backplane
        // (IHubContext<NotificationsHub>), never accepts a connection itself.
        services.AddSignalR().AddStackExchangeRedis();
        services.AddOptions<RedisOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                var connectionString = configuration["ConnectionStrings:Redis"]
                    ?? throw new InvalidOperationException("Missing ConnectionStrings:Redis.");
                options.Configuration = ConfigurationOptions.Parse(connectionString);
            });
        services.AddSingleton<IUserIdProvider, JwtSubUserIdProvider>();
        services.AddScoped<INotificationBroadcaster, SignalRNotificationBroadcaster>();

        services.AddOptions<PushOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                options.VapidSubject = configuration["Push:VapidSubject"]
                    ?? throw new InvalidOperationException("Missing Push:VapidSubject.");
                options.VapidPublicKey = configuration["Push:VapidPublicKey"]
                    ?? throw new InvalidOperationException("Missing Push:VapidPublicKey.");
                options.VapidPrivateKey = configuration["Push:VapidPrivateKey"]
                    ?? throw new InvalidOperationException("Missing Push:VapidPrivateKey.");
            });
        services.AddScoped<IPushNotificationSender, WebPushNotificationSender>();

        // The one half of PushOptions the Application layer (GetVapidPublicKeyQuery) is allowed
        // to see — bound separately so Application never references Infrastructure's PushOptions.
        services.AddOptions<PushPublicKeyOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                options.VapidPublicKey = configuration["Push:VapidPublicKey"]
                    ?? throw new InvalidOperationException("Missing Push:VapidPublicKey."));

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
        services.AddScoped<IAccessPolicy, AccessPolicy>();
        services.AddScoped<IBulkImportFileReader, BulkImportFileReader>();
        services.AddScoped<IBulkImportTemplateGenerator, BulkImportTemplateGenerator>();

        services.AddOptions<StorageOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                options.Endpoint = configuration["Storage:Endpoint"]
                    ?? throw new InvalidOperationException("Missing Storage:Endpoint.");
                options.Bucket = configuration["Storage:Bucket"]
                    ?? throw new InvalidOperationException("Missing Storage:Bucket.");
                options.AccessKey = configuration["Storage:AccessKey"]
                    ?? throw new InvalidOperationException("Missing Storage:AccessKey.");
                options.SecretKey = configuration["Storage:SecretKey"]
                    ?? throw new InvalidOperationException("Missing Storage:SecretKey.");
                options.Region = configuration["Storage:Region"] ?? "auto";
                options.AutoCreateBucket = configuration.GetValue("Storage:AutoCreateBucket", false);
            });

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var storageOptions = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            var config = new AmazonS3Config
            {
                ServiceURL = storageOptions.Endpoint,
                ForcePathStyle = true,
                AuthenticationRegion = storageOptions.Region == "auto" ? "us-east-1" : storageOptions.Region,
            };
            return new AmazonS3Client(storageOptions.AccessKey, storageOptions.SecretKey, config);
        });

        services.AddScoped<IFileStorage, S3FileStorage>();
        services.AddHostedService<S3BucketInitializer>();

        services.AddOptions<ClamAvOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                options.Host = configuration["ClamAV:Host"]
                    ?? throw new InvalidOperationException("Missing ClamAV:Host.");

                var port = configuration["ClamAV:Port"];
                if (port is not null)
                {
                    options.Port = int.Parse(port, CultureInfo.InvariantCulture);
                }
            });

        services.AddScoped<IVirusScanner, ClamAvVirusScanner>();

        QuestPDF.Settings.License = LicenseType.Community;
        services.AddScoped<IPdfRenderer, QuestPdfReportReceiptRenderer>();
        services.AddScoped<IExcelRenderer, ClosedXmlExcelRenderer>();
        services.AddScoped<ICompiledReportRenderer, CompiledReportRenderer>();
        services.AddScoped<IRequestContext, NoRequestContext>();
        services.AddScoped<IJobQueueMonitor, HangfireJobQueueMonitor>();
        services.AddSingleton<IEmailTransportInfo, EmailTransportInfo>();

        // FR-ADM-003: outgoing webhooks — 10 s timeout, no redirects, public addresses only.
        services.AddOptions<WebhookOptions>().BindConfiguration("Webhooks");
        services.AddHttpClient(HttpWebhookSender.ClientName, client => client.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(sp =>
                HttpWebhookSender.CreateHandler(sp.GetRequiredService<IOptions<WebhookOptions>>().Value.AllowPrivateNetworks));
        services.AddScoped<IWebhookSender, HttpWebhookSender>();

        // FR-HIS-004: the API only ever enqueues (client-side); the Worker's own Program.cs adds
        // AddHangfireServer() on top of this same registration to actually process jobs
        // (architecture.md: "Worker: Hangfire server... exports..."). The (provider, config)
        // overload defers reading the connection string until the container can resolve
        // IConfiguration — AddInfrastructure itself has no IConfiguration parameter to read it
        // eagerly the way the Worker's old Program.cs code did.
        services.AddHangfire((provider, config) => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(GetConnectionString(provider, "Default"))));
        services.AddScoped<BulkExportJob>();
        services.AddScoped<IBackgroundJobScheduler, HangfireBackgroundJobScheduler>();

        return services;
    }

    private static string GetConnectionString(IServiceProvider services, string name) =>
        services.GetRequiredService<IConfiguration>()[$"ConnectionStrings:{name}"]
            ?? throw new InvalidOperationException($"Missing ConnectionStrings:{name}.");
}
