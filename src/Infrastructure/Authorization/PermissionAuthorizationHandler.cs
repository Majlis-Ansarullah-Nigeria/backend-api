using ManagementApi.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace ManagementApi.Infrastructure.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Check if user is authenticated
        if (context.User?.Identity == null || !context.User.Identity.IsAuthenticated)
        {
            return Task.CompletedTask;
        }

        // Admin role has all permissions
        if (context.User.IsInRole(Roles.Admin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check if user has the required permission
        var permissions = context.User.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToList();

        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
