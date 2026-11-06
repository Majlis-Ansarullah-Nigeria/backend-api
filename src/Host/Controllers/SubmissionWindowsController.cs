using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Commands;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Application.Reports.Queries;
using ManagementApi.Infrastructure.Authorization;
using ManagementApi.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementApi.Host.Controllers;

[Authorize]
public class SubmissionWindowsController : BaseApiController
{
    // Windows and extensions are set by the National Secretariat.
    private const string SecretariatRoles = Roles.NationalAdmin + "," + Roles.SuperAdmin;

    /// <summary>
    /// Get submission windows with optional filtering by template
    /// </summary>
    [HttpGet]
    [MustHavePermission(Permissions.ReportsView)]
    [ProducesResponseType(typeof(List<SubmissionWindowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubmissionWindows([FromQuery] Guid? reportTemplateId)
    {
        var result = await Mediator.Send(new GetSubmissionWindowsQuery(reportTemplateId));
        return Ok(result);
    }

    /// <summary>
    /// Create a new submission window
    /// </summary>
    [HttpPost]
    [MustHavePermission(Permissions.ReportTemplatesCreate)]
    [Authorize(Roles = SecretariatRoles)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSubmissionWindow([FromBody] CreateSubmissionWindowRequest request)
    {
        var result = await Mediator.Send(new CreateSubmissionWindowCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { id = result.Data, message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Update a submission window
    /// </summary>
    [HttpPut("{id}")]
    [MustHavePermission(Permissions.ReportTemplatesEdit)]
    [Authorize(Roles = SecretariatRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSubmissionWindow(Guid id, [FromBody] UpdateSubmissionWindowRequest request)
    {
        var result = await Mediator.Send(new UpdateSubmissionWindowCommand(id, request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Give one unit more time in a window (replaces any earlier extension for that unit). National Secretariat only.
    /// </summary>
    [HttpPost("{id}/extensions")]
    [Authorize(Roles = SecretariatRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GrantExtension(Guid id, [FromBody] GrantExtensionRequest request)
    {
        var result = await Mediator.Send(new GrantExtensionCommand(id, request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { id = result.Data, message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Remove a unit's extension. National Secretariat only.
    /// </summary>
    [HttpDelete("{id}/extensions/{extensionId}")]
    [Authorize(Roles = SecretariatRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveExtension(Guid id, Guid extensionId)
    {
        var result = await Mediator.Send(new RemoveExtensionCommand(id, extensionId));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }
}
