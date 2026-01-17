using Hangfire.Dashboard;

namespace ManagementApi.Host.Filters;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Allow all authenticated users to access Hangfire Dashboard
        // In production, you should restrict this to specific roles/permissions
        return httpContext.User.Identity?.IsAuthenticated ?? false;

        // Alternative: Allow only in Development
        // var env = httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        // return env.IsDevelopment();
    }
}
