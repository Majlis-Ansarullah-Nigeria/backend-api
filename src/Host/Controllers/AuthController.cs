using ManagementApi.Application.Auth.Commands;
using ManagementApi.Application.Auth.DTOs;
using ManagementApi.Application.Auth.Queries;
using ManagementApi.Application.Auth.Recovery;
using ManagementApi.Application.Common.Models;
using ManagementApi.Domain.Identity;
using ManagementApi.Shared.Authorization;
using Microsoft.AspNetCore.RateLimiting;
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

    // ----- Forgot password (anonymous, rate limited; never reveals whether a membership number exists) -----

    /// <summary>
    /// Step 1: shows the email on record, masked, so the member can choose to use it or ask for a new one.
    /// </summary>
    [HttpPost("forgot-password/lookup")]
    [AllowAnonymous]
    [EnableRateLimiting("account-recovery")]
    [ProducesResponseType(typeof(ForgotPasswordLookupResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPasswordLookup([FromBody] ForgotPasswordLookupRequest request)
    {
        var result = await Mediator.Send(new ForgotPasswordLookupQuery(request));
        return Ok(result.Data);
    }

    /// <summary>
    /// Step 2a: sends a reset link (1 hour, single use) to the email on record.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("account-recovery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await Mediator.Send(new SendPasswordResetLinkCommand(request));
        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Step 2b: asks the National Secretariat to put a new email on the account; once approved the reset link goes there.
    /// </summary>
    [HttpPost("email-change-requests")]
    [AllowAnonymous]
    [EnableRateLimiting("account-recovery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestEmailChange([FromBody] RequestEmailChangeRequest request)
    {
        var result = await Mediator.Send(new RequestEmailChangeCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Step 3: sets the new password using the link from the email.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("account-recovery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await Mediator.Send(new ResetPasswordCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    // ----- Email change requests (National Secretariat) -----

    /// <summary>
    /// Email change requests, newest first. Filter by status: Pending, Approved, Rejected.
    /// </summary>
    [HttpGet("email-change-requests")]
    [Authorize(Roles = Roles.NationalAdmin + "," + Roles.SuperAdmin)]
    [ProducesResponseType(typeof(List<EmailChangeRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmailChangeRequests([FromQuery] EmailChangeRequestStatus? status = EmailChangeRequestStatus.Pending)
    {
        return FromResult(await Mediator.Send(new GetEmailChangeRequestsQuery(status)));
    }

    /// <summary>
    /// Number of pending email change requests, for the menu badge.
    /// </summary>
    [HttpGet("email-change-requests/pending-count")]
    [Authorize(Roles = Roles.NationalAdmin + "," + Roles.SuperAdmin)]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingEmailChangeCount()
    {
        return FromResult(await Mediator.Send(new GetPendingEmailChangeCountQuery()));
    }

    /// <summary>
    /// Approve: updates the member's email, tells the old and new addresses, and sends a reset link to the new one.
    /// </summary>
    [HttpPost("email-change-requests/{id}/approve")]
    [Authorize(Roles = Roles.NationalAdmin + "," + Roles.SuperAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveEmailChange(Guid id)
    {
        var result = await Mediator.Send(new ApproveEmailChangeCommand(id));
        return result.Succeeded ? Ok(new { message = result.Messages.FirstOrDefault() }) : BadRequest(new { errors = result.Messages });
    }

    /// <summary>
    /// Reject an email change request, with an optional reason.
    /// </summary>
    [HttpPost("email-change-requests/{id}/reject")]
    [Authorize(Roles = Roles.NationalAdmin + "," + Roles.SuperAdmin)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectEmailChange(Guid id, [FromBody] RejectEmailChangeRequest request)
    {
        var result = await Mediator.Send(new RejectEmailChangeCommand(id, request));
        return result.Succeeded ? Ok(new { message = result.Messages.FirstOrDefault() }) : BadRequest(new { errors = result.Messages });
    }

    private IActionResult FromResult<T>(Result<T> result) =>
        result.Succeeded ? Ok(result.Data) : StatusCode(StatusCodes.Status403Forbidden, new { errors = result.Messages });
}
