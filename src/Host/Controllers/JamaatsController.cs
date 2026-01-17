using ManagementApi.Application.Jamaats.Commands;
using ManagementApi.Application.Jamaats.DTOs;
using ManagementApi.Application.Jamaats.Queries;
using ManagementApi.Infrastructure.Authorization;
using ManagementApi.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementApi.Host.Controllers;

[Authorize]
public class JamaatsController : BaseApiController
{
    /// <summary>
    /// Get all Jamaats with optional filter for unmapped only
    /// </summary>
    [HttpGet]
    [MustHavePermission(Permissions.JamaatsView)]
    [ProducesResponseType(typeof(List<JamaatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJamaats([FromQuery] bool? onlyUnmapped = null)
    {
        var result = await Mediator.Send(new GetJamaatsQuery(onlyUnmapped));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Get Jamaat mapping statistics
    /// </summary>
    [HttpGet("stats")]
    [MustHavePermission(Permissions.JamaatsView)]
    [ProducesResponseType(typeof(JamaatMappingStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMappingStats()
    {
        var result = await Mediator.Send(new GetJamaatMappingStatsQuery());

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Map a Jamaat to a Muqam (removes from previous Muqam if already mapped)
    /// </summary>
    [HttpPost("map")]
    [MustHavePermission(Permissions.JamaatsMap)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MapJamaatToMuqam([FromBody] MapJamaatToMuqamRequest request)
    {
        var result = await Mediator.Send(new MapJamaatToMuqamCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Unmap a Jamaat from its current Muqam
    /// </summary>
    [HttpPost("{jamaatId}/unmap")]
    [MustHavePermission(Permissions.JamaatsUnmap)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnmapJamaat(Guid jamaatId)
    {
        var request = new UnmapJamaatRequest { JamaatId = jamaatId };
        var result = await Mediator.Send(new UnmapJamaatCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Sync jamaats from external API (Tajneed Gateway)
    /// Fetches jamaats from https://tajneedapi.ahmadiyyanigeria.net/jamaats
    /// and adds new jamaats or updates existing ones
    /// </summary>
    [HttpPost("sync")]
    [MustHavePermission(Permissions.JamaatsView)]
    [ProducesResponseType(typeof(JamaatSyncResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SyncJamaats()
    {
        var result = await Mediator.Send(new FetchJamaatsFromApiCommand());

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
}
