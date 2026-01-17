using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Users.Commands;
using ManagementApi.Application.Users.DTOs;
using ManagementApi.Application.Users.Queries;
using ManagementApi.Infrastructure.Authorization;
using ManagementApi.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementApi.Host.Controllers;

[Authorize]
public class UsersController : BaseApiController
{
    /// <summary>
    /// Get all users with pagination and search
    /// </summary>
    [HttpGet]
    [MustHavePermission(Permissions.UsersView)]
    [ProducesResponseType(typeof(PaginationResponse<UserWithRolesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null)
    {
        var result = await Mediator.Send(new GetUsersQuery(pageNumber, pageSize, searchTerm));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Assign roles to a user (Admin only)
    /// </summary>
    [HttpPost("{userId}/roles")]
    [MustHavePermission(Permissions.UsersAssignRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AssignRoles(Guid userId, [FromBody] List<string> roles)
    {
        var request = new AssignRolesRequest { UserId = userId, Roles = roles };
        var result = await Mediator.Send(new AssignRolesCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }
}
