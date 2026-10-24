using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using HRS.Api.Common;
using HRS.Api.Endpoints;
using HRS.Application;
using HRS.Application.Common.Interfaces;
using HRS.Infrastructure;
using HRS.Infrastructure.Notifications;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

    builder.Services.AddOpenApi();
    builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IRequestContext, HttpRequestContext>();

    // FR-AUD-001 records the client's IP. Behind Dokploy's proxy the TCP peer is the proxy, so
    // read X-Forwarded-For — but only from proxies on the networks configured here (private
    // container networks by default), so a client can't forge its own address.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var network in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
        {
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        }
    });

    var signingKey = builder.Configuration["Auth:SigningKey"]
        ?? throw new InvalidOperationException("Missing Auth:SigningKey.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ClockSkew = TimeSpan.FromSeconds(30),
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    context.Token = context.Request.Cookies[AuthCookies.AccessToken];
                    return Task.CompletedTask;
                },
            };
        });
    builder.Services.AddAuthorization();

    // NFR-SEC-04: rate limiting on auth endpoints. A single API instance today (see
    // docker-compose.dokploy.yml), so an in-memory limiter is enough; the per-identifier
    // OTP request throttle in IdentityService uses Redis separately since it must key
    // off the request body, which runs before this middleware.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = (context, cancellationToken) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            }

            return ValueTask.CompletedTask;
        };

        // 750/5min per IP: still a meaningful anti-abuse ceiling (well below credential-stuffing
        // rates) but with headroom for the growing integration-test suite, which shares one
        // in-memory limiter across every test in the "Api" collection (ApiFactory.cs). Raised from
        // 300 when M5.4's attachment tests pushed the full suite past the old ceiling, then from
        // 600 when M9.2's export tests (each logging in its own secretariat + unit-head client)
        // did the same — bump this again if that recurs.
        var authPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:Auth:PermitLimit") ?? 750;

        options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(5),
                PermitLimit = authPermitLimit,
                QueueLimit = 0,
            }));
    });

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapOpenApi();
    app.MapScalarApiReference();
    app.MapGet("/scalar", () => Results.Redirect("/scalar/v1"));

    app.MapHealthChecks("/api/health", new HealthCheckOptions
    {
        ResponseWriter = HealthCheckResponseWriter.Write,
    });

    app.MapIdentityEndpoints();
    app.MapInvitationsEndpoints();
    app.MapOrganizationEndpoints();
    app.MapAdminEndpoints();
    app.MapTemplatesEndpoints();
    app.MapWindowsEndpoints();
    app.MapWindowSchedulesEndpoints();
    app.MapSubmissionsEndpoints();
    app.MapObserverEndpoints();
    app.MapReviewEndpoints();
    app.MapDepartmentEndpoints();
    app.MapNotificationsEndpoints();
    app.MapHistoryEndpoints();
    app.MapDashboardsEndpoints();
    app.MapCompiledReportsEndpoints();
    app.MapAuditEndpoints();
    app.MapSettingsEndpoints();
    app.MapExportsEndpoints();

    // architecture.md: Traefik routes "/hubs/*" to this process only — the Worker publishes
    // through it via the Redis backplane (IHubContext<NotificationsHub>) but never hosts it.
    app.MapHub<NotificationsHub>("/hubs/notifications").RequireAuthorization();

    app.MapGet("/", () => "HRS API");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "HRS.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
