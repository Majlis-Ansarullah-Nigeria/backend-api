using ManagementApi.Infrastructure.Authorization;
using ManagementApi.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementApi.Host.Controllers;

[Authorize]
public class RolesController : BaseApiController
{
    /// <summary>
    /// Get all available roles
    /// </summary>
    [HttpGet]
    [MustHavePermission(Permissions.RolesView)]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public IActionResult GetRoles()
    {
        var roles = Roles.GetDefaultRoles();
        return Ok(roles);
    }

    /// <summary>
    /// Get all available permissions
    /// </summary>
    [HttpGet("permissions")]
    [MustHavePermission(Permissions.RolesView)]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public IActionResult GetPermissions()
    {
        var permissions = Permissions.GetAllPermissions();
        return Ok(permissions);
    }

    /// <summary>
    /// Get permissions for a specific role
    /// </summary>
    [HttpGet("{roleName}/permissions")]
    [MustHavePermission(Permissions.RolesView)]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public IActionResult GetRolePermissions(string roleName)
    {
        var rolePermissions = Roles.GetDefaultRolePermissions();

        if (rolePermissions.TryGetValue(roleName, out var permissions))
        {
            return Ok(permissions);
        }

        return Ok(new List<string>());
    }
}
