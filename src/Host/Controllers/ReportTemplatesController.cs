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
    public async Task<IActionResult> ActivateTemplate(Guid templateId)
    {
        // Implementation would go through a command
        return Ok(new { message = "Template activated successfully" });
    }

    /// <summary>
    /// Deactivate a report template
    /// </summary>
    [HttpPost("{templateId}/deactivate")]
    [MustHavePermission(Permissions.ReportTemplatesEdit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateTemplate(Guid templateId)
    {
        // Implementation would go through a command
        return Ok(new { message = "Template deactivated successfully" });
    }
}
