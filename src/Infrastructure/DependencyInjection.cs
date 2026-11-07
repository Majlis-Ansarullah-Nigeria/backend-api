using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Domain.Identity;
using ManagementApi.Infrastructure.Authorization;
using ManagementApi.Infrastructure.Identity;
using ManagementApi.Infrastructure.Persistence;
using ManagementApi.Infrastructure.Persistence.Repositories;
using ManagementApi.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ManagementApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        // Server clock: decides whether submission windows are open (BR-WIN-07)
        services.AddSingleton(TimeProvider.System);

        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped(typeof(IReadRepository<>), typeof(ReadRepository<>));

        // Identity
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            // Password settings - SIMPLIFIED as per requirements
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 3; // Minimum 3 characters
            options.Password.RequiredUniqueChars = 0;

            // User settings
            options.User.RequireUniqueEmail = false; // Members may not have emails

            // Lockout settings
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // Password reset links expire after 1 hour
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(1));
        services.AddMemoryCache();

        // Services
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<Services.IEmailTemplateService, Services.EmailTemplateService>();
        services.AddSingleton<ManagementApi.Application.Reports.Pdf.IReportPdfRenderer, Reports.ReportPdfRenderer>();
        services.AddScoped<IEmailService, Services.MailKitEmailService>();
        services.AddScoped<INotificationService, Services.NotificationService>();
        services.AddScoped<IAccountRecoveryService, AccountRecoveryService>();

        // External Services
        services.AddHttpClient();
        services.AddScoped<IExternalMembersService, ExternalServices.ExternalMembersService>();
        services.AddScoped<IExternalJamaatsService, ExternalServices.ExternalJamaatsService>();

        // Background Jobs
        services.AddScoped<IMemberSyncJob, BackgroundJobs.MemberSyncJob>();
        services.AddScoped<IJamaatSyncJob, BackgroundJobs.JamaatSyncJob>();
        services.AddScoped<BackgroundJobs.IDeadlineReminderJob, BackgroundJobs.DeadlineReminderJob>();
        services.AddScoped<BackgroundJobs.IMissedReportsJob, BackgroundJobs.MissedReportsJob>();

        // Authorization
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        // Add authorization policies for each permission
        services.AddAuthorization(options =>
        {
            var allPermissions = Permissions.GetAllPermissions();

            foreach (var permission in allPermissions)
            {
                options.AddPolicy(permission, policy =>
                    policy.Requirements.Add(new PermissionRequirement(permission)));
            }
        });

        // Database Seeder
        services.AddScoped<Persistence.Seeders.DatabaseSeeder>();

        return services;
    }
}
