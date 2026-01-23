using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Members.Commands;
using ManagementApi.Application.Members.DTOs;
using ManagementApi.Application.Members.Queries;
using ManagementApi.Infrastructure.Authorization;
using ManagementApi.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementApi.Host.Controllers;

[Authorize]
public class MembersController : BaseApiController
{
    /// <summary>
    /// Get members filtered by current user's organization level
    /// </summary>
    [HttpGet]
    [MustHavePermission(Permissions.MembersView)]
    [ProducesResponseType(typeof(PaginationResponse<MemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var result = await Mediator.Send(new GetMembersQuery(pageNumber, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// Get member profile by Chanda Number
    /// Users can only view their own profile unless they have elevated permissions
    /// </summary>
    [HttpGet("profile/{chandaNo}")]
    [MustHavePermission(Permissions.MembersView)]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMemberProfile(string chandaNo)
    {
        var result = await Mediator.Send(new GetMemberProfileQuery(chandaNo));

        if (!result.Succeeded)
        {
            if (result.Messages.Any(m => m.Contains("not found")))
            {
                return NotFound(new { errors = result.Messages });
            }
            return StatusCode(StatusCodes.Status403Forbidden, new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Search members with filters and pagination
    /// </summary>
    [HttpPost("search")]
    [MustHavePermission(Permissions.MembersView)]
    [ProducesResponseType(typeof(PaginationResponse<MemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchMembers([FromBody] SearchMembersRequest request)
    {
        var result = await Mediator.Send(new SearchMembersQuery(request));
        return Ok(result);
    }

    /// <summary>
    /// Update member photo URL
    /// </summary>
    [HttpPut("photo")]
    [MustHavePermission(Permissions.MembersEdit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMemberPhoto([FromBody] UpdateMemberPhotoRequest request)
    {
        var result = await Mediator.Send(new UpdateMemberPhotoCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Update member signature URL
    /// </summary>
    [HttpPut("signature")]
    [MustHavePermission(Permissions.MembersEdit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMemberSignature([FromBody] UpdateMemberSignatureRequest request)
    {
        var result = await Mediator.Send(new UpdateMemberSignatureCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Generate QR code for a member
    /// </summary>
    [HttpGet("{chandaNo}/qrcode")]
    [MustHavePermission(Permissions.MembersView)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateMemberQRCode(string chandaNo)
    {
        var result = await Mediator.Send(new GenerateMemberQRCodeQuery(chandaNo));

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Messages });
        }

        return File(result.Data!, "image/png", $"{chandaNo}_qrcode.png");
    }

    /// <summary>
    /// Sync members from external API (Tajneed Gateway)
    /// Fetches members from https://tajneedapi.ahmadiyyanigeria.net/members/auxilliarybody/ansarullah
    /// and adds new members or updates existing ones
    /// </summary>
    [HttpPost("sync")]
    [MustHavePermission(Permissions.MembersEdit)]
    [ProducesResponseType(typeof(MemberSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SyncMembers()
    {
        var result = await Mediator.Send(new FetchMembersFromGatewayCommand());

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new
        {
            message = result.Messages.FirstOrDefault(),
            data = result.Data
        });
    }

    /// <summary>
    /// Get all positions for a member
    /// </summary>
    [HttpGet("{memberId}/positions")]
    [MustHavePermission(Permissions.MembersView)]
    [ProducesResponseType(typeof(List<MemberPositionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMemberPositions(Guid memberId, [FromQuery] bool? activeOnly = null)
    {
        var result = await Mediator.Send(new GetMemberPositionsQuery(memberId, activeOnly));
        return Ok(result);
    }

    /// <summary>
    /// Create a new position for a member
    /// </summary>
    [HttpPost("positions")]
    [MustHavePermission(Permissions.MembersEdit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateMemberPosition([FromBody] CreateMemberPositionRequest request)
    {
        var result = await Mediator.Send(new CreateMemberPositionCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new
        {
            message = result.Messages.FirstOrDefault(),
            positionId = result.Data
        });
    }

    /// <summary>
    /// Update an existing member position
    /// </summary>
    [HttpPut("positions")]
    [MustHavePermission(Permissions.MembersEdit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMemberPosition([FromBody] UpdateMemberPositionRequest request)
    {
        var result = await Mediator.Send(new UpdateMemberPositionCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// End a member position
    /// </summary>
    [HttpPut("positions/end")]
    [MustHavePermission(Permissions.MembersEdit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EndMemberPosition([FromBody] EndMemberPositionRequest request)
    {
        var result = await Mediator.Send(new EndMemberPositionCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Get comprehensive member profile data for export (PDF/Excel)
    /// </summary>
    [HttpGet("{memberId}/export-data")]
    [MustHavePermission(Permissions.MembersView)]
    [ProducesResponseType(typeof(MemberProfileExportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMemberProfileForExport(Guid memberId)
    {
        var result = await Mediator.Send(new GetMemberProfileForExportQuery(memberId));

        if (result == null)
        {
            return NotFound(new { message = "Member not found" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Update member information
    /// </summary>
    [HttpPut]
    [MustHavePermission(Permissions.MembersEdit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMember([FromBody] UpdateMemberRequest request)
    {
        var result = await Mediator.Send(new UpdateMemberCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Get comprehensive member statistics
    /// </summary>
    [HttpGet("statistics")]
    [MustHavePermission(Permissions.MembersView)]
    [ProducesResponseType(typeof(MemberStatisticsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMemberStatistics()
    {
        var result = await Mediator.Send(new GetMemberStatisticsQuery());
        return Ok(result);
    }
}
