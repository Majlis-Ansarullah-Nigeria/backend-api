using ManagementApi.Application.Roles.Commands;
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

    /// <summary>
    /// Update permissions for a specific role
    /// </summary>
    [HttpPut("{roleName}/permissions")]
    [MustHavePermission(Permissions.RolesManagePermissions)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRolePermissions(string roleName, [FromBody] List<string> permissions)
    {
        var result = await Mediator.Send(new UpdateRolePermissionsCommand(roleName, permissions));

        if (!result.Succeeded)
        {
            if (result.Messages.Contains("not found"))
            {
                return NotFound(new { errors = result.Messages });
            }
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }
}
