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

        // SuperAdmin and NationalAdmin roles have all permissions
        if (context.User.IsInRole(Roles.SuperAdmin) ||
            context.User.IsInRole(Roles.NationalAdmin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check if user has the required permission in claims
        // NOTE: Case-insensitive comparison to handle database storing permissions in different casing
        var permissions = context.User.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToList();

        if (permissions.Any(p => string.Equals(p, requirement.Permission, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
