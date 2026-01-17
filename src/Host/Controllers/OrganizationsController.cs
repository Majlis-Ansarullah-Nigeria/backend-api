using ManagementApi.Application.Organizations.Commands;
using ManagementApi.Application.Organizations.DTOs;
using ManagementApi.Application.Organizations.Queries;
using ManagementApi.Infrastructure.Authorization;
using ManagementApi.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementApi.Host.Controllers;

[Authorize]
public class OrganizationsController : BaseApiController
{
    /// <summary>
    /// Get all Muqams with statistics
    /// </summary>
    [HttpGet("muqams")]
    [MustHavePermission(Permissions.OrganizationsView)]
    [ProducesResponseType(typeof(List<MuqamDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMuqams()
    {
        var result = await Mediator.Send(new GetMuqamsQuery());

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Create a new Muqam
    /// </summary>
    [HttpPost("muqams")]
    [MustHavePermission(Permissions.OrganizationsCreate)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateMuqam([FromBody] CreateMuqamRequest request)
    {
        var result = await Mediator.Send(new CreateMuqamCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { id = result.Data, message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Get all Dilas with statistics
    /// </summary>
    [HttpGet("dilas")]
    [MustHavePermission(Permissions.OrganizationsView)]
    [ProducesResponseType(typeof(List<DilaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDilas()
    {
        var result = await Mediator.Send(new GetDilasQuery());

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Get all Zones with statistics
    /// </summary>
    [HttpGet("zones")]
    [MustHavePermission(Permissions.OrganizationsView)]
    [ProducesResponseType(typeof(List<ZoneDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetZones()
    {
        var result = await Mediator.Send(new GetZonesQuery());

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Get organization statistics for dashboard
    /// </summary>
    [HttpGet("statistics")]
    [MustHavePermission(Permissions.OrganizationsView)]
    [ProducesResponseType(typeof(OrganizationStatisticsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatistics()
    {
        var result = await Mediator.Send(new GetOrganizationStatisticsQuery());

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Get Muqam by ID
    /// </summary>
    [HttpGet("muqams/{id}")]
    [MustHavePermission(Permissions.OrganizationsView)]
    [ProducesResponseType(typeof(MuqamDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMuqamById(Guid id)
    {
        var result = await Mediator.Send(new GetMuqamByIdQuery(id));

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Update a Muqam
    /// </summary>
    [HttpPut("muqams/{id}")]
    [MustHavePermission(Permissions.OrganizationsEdit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMuqam(Guid id, [FromBody] UpdateMuqamRequest request)
    {
        if (id != request.Id)
        {
            return BadRequest(new { errors = new[] { "ID mismatch" } });
        }

        var result = await Mediator.Send(new UpdateMuqamCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Create a new Dila
    /// </summary>
    [HttpPost("dilas")]
    [MustHavePermission(Permissions.OrganizationsCreate)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateDila([FromBody] CreateDilaRequest request)
    {
        var result = await Mediator.Send(new CreateDilaCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { id = result.Data, message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Create a new Zone
    /// </summary>
    [HttpPost("zones")]
    [MustHavePermission(Permissions.OrganizationsCreate)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateZone([FromBody] CreateZoneRequest request)
    {
        var result = await Mediator.Send(new CreateZoneCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { id = result.Data, message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Get Dila by ID
    /// </summary>
    [HttpGet("dilas/{id}")]
    [MustHavePermission(Permissions.OrganizationsView)]
    [ProducesResponseType(typeof(DilaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDilaById(Guid id)
    {
        var result = await Mediator.Send(new GetDilaByIdQuery(id));

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Update a Dila
    /// </summary>
    [HttpPut("dilas/{id}")]
    [MustHavePermission(Permissions.OrganizationsEdit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateDila(Guid id, [FromBody] UpdateDilaRequest request)
    {
        if (id != request.Id)
        {
            return BadRequest(new { errors = new[] { "ID mismatch" } });
        }

        var result = await Mediator.Send(new UpdateDilaCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Get Zone by ID
    /// </summary>
    [HttpGet("zones/{id}")]
    [MustHavePermission(Permissions.OrganizationsView)]
    [ProducesResponseType(typeof(ZoneDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetZoneById(Guid id)
    {
        var result = await Mediator.Send(new GetZoneByIdQuery(id));

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Update a Zone
    /// </summary>
    [HttpPut("zones/{id}")]
    [MustHavePermission(Permissions.OrganizationsEdit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateZone(Guid id, [FromBody] UpdateZoneRequest request)
    {
        if (id != request.Id)
        {
            return BadRequest(new { errors = new[] { "ID mismatch" } });
        }

        var result = await Mediator.Send(new UpdateZoneCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Delete a Muqam
    /// </summary>
    [HttpDelete("muqams/{id}")]
    [MustHavePermission(Permissions.OrganizationsDelete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteMuqam(Guid id)
    {
        var result = await Mediator.Send(new DeleteMuqamCommand(id));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Delete a Dila
    /// </summary>
    [HttpDelete("dilas/{id}")]
    [MustHavePermission(Permissions.OrganizationsDelete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteDila(Guid id)
    {
        var result = await Mediator.Send(new DeleteDilaCommand(id));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Delete a Zone
    /// </summary>
    [HttpDelete("zones/{id}")]
    [MustHavePermission(Permissions.OrganizationsDelete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteZone(Guid id)
    {
        var result = await Mediator.Send(new DeleteZoneCommand(id));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Bulk assign Muqams to a Dila
    /// </summary>
    [HttpPost("muqams/bulk-assign")]
    [MustHavePermission(Permissions.OrganizationsEdit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkAssignMuqams([FromBody] BulkAssignMuqamsRequest request)
    {
        var result = await Mediator.Send(new BulkAssignMuqamsCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { count = result.Data, message = result.Messages.FirstOrDefault() });
    }
}
