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
}
