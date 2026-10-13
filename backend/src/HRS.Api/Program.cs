using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using HRS.Api.Common;
using HRS.Api.Endpoints;
using HRS.Application;
using HRS.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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

        // 600/5min per IP: still a meaningful anti-abuse ceiling (well below credential-stuffing
        // rates) but with headroom for the growing integration-test suite, which shares one
        // in-memory limiter across every test in the "Api" collection (ApiFactory.cs). Raised from
        // 300 when M5.4's attachment tests (each logging in its own secretariat + unit-head client)
        // pushed the full suite past the old ceiling — bump this again if that recurs.
        var authPermitLimit = builder.Configuration.GetValue<int?>("RateLimiting:Auth:PermitLimit") ?? 600;

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
