using ManagementApi.Application.Reports.Commands;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Application.Reports.Queries;
using ManagementApi.Infrastructure.Authorization;
using ManagementApi.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementApi.Host.Controllers;

[Authorize]
public class ReportTemplatesController : BaseApiController
{
    /// <summary>
    /// Get all report templates
    /// </summary>
    [HttpGet]
    [MustHavePermission(Permissions.ReportTemplatesView)]
    [ProducesResponseType(typeof(List<ReportTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReportTemplates([FromQuery] bool? onlyActive = null)
    {
        var result = await Mediator.Send(new GetReportTemplatesQuery(onlyActive));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Get a specific report template by ID (for users with submit permission)
    /// </summary>
    [HttpGet("accessible/{templateId}")]
    [MustHavePermission(Permissions.ReportsSubmit)]
    [ProducesResponseType(typeof(ReportTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserAccessibleTemplate(Guid templateId)
    {
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(currentUserId, out Guid userId))
        {
            return BadRequest(new { errors = new[] { "Invalid user ID" } });
        }

        var result = await Mediator.Send(new GetUserAccessibleTemplateQuery(templateId, userId));

        if (!result.Succeeded)
        {
            if (result.Messages.Contains("not found") || result.Messages.Contains("User not found"))
            {
                return NotFound(new { errors = result.Messages });
            }
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Get a specific report template by ID (for users with view permission)
    /// </summary>
    [HttpGet("{templateId}")]
    [MustHavePermission(Permissions.ReportTemplatesView)]
    [ProducesResponseType(typeof(ReportTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReportTemplateById(Guid templateId)
    {
        var result = await Mediator.Send(new GetReportTemplateByIdQuery(templateId));

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Create a new report template
    /// </summary>
    [HttpPost]
    [MustHavePermission(Permissions.ReportTemplatesCreate)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReportTemplate([FromBody] CreateReportTemplateRequest request)
    {
        var result = await Mediator.Send(new CreateReportTemplateCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { id = result.Data, message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Activate a report template
    /// </summary>
    [HttpPost("{templateId}/activate")]
    [MustHavePermission(Permissions.ReportTemplatesEdit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActivateTemplate(Guid templateId)
    {
        var result = await Mediator.Send(new ActivateTemplateCommand(templateId));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Deactivate a report template (US-3: Validates no active windows exist)
    /// </summary>
    [HttpPost("{templateId}/deactivate")]
    [MustHavePermission(Permissions.ReportTemplatesEdit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeactivateTemplate(Guid templateId)
    {
        var result = await Mediator.Send(new DeactivateTemplateCommand(templateId));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }
}
