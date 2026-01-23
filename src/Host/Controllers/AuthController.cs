using ManagementApi.Application.Auth.Commands;
using ManagementApi.Application.Auth.DTOs;
using ManagementApi.Application.Auth.Queries;
using ManagementApi.Infrastructure.Persistence.Seeders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementApi.Host.Controllers;

public class AuthController : BaseApiController
{
    private readonly DatabaseSeeder _seeder;

    public AuthController(DatabaseSeeder seeder)
    {
        _seeder = seeder;
    }
    /// <summary>
    /// Register a new user with ChandaNo (Membership Number) and Password
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await Mediator.Send(new RegisterCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Login with ChandaNo (Membership Number) and Password
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await Mediator.Send(new LoginQuery(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Get current authenticated user information
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var result = await Mediator.Send(new GetCurrentUserQuery());

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Manually seed database with roles and admin users (Development only)
    /// </summary>
    [HttpPost("seed")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SeedDatabase()
    {
        try
        {
            await _seeder.SeedAsync();
            return Ok(new
            {
                message = "Database seeded successfully",
                credentials = new
                {
                    superAdmin = new { chandaNo = "SUPERADMIN", password = "SuperAdmin@123" },
                    nationalAdmin = new { chandaNo = "ADMIN001", password = "Admin@123" }
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { errors = new[] { ex.Message } });
        }
    }

    /// <summary>
    /// Debug endpoint to view current user's claims and permissions
    /// </summary>
    [HttpGet("debug/claims")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetClaims()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
        var roles = User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
        var permissions = User.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();

        return Ok(new
        {
            isAuthenticated = User.Identity?.IsAuthenticated,
            userName = User.Identity?.Name,
            roles,
            permissions,
            permissionCount = permissions.Count,
            allClaims = claims
        });
    }
}
