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
public class ReportSubmissionsController : BaseApiController
{
    /// <summary>
    /// Get report submissions with filtering
    /// </summary>
    [HttpGet]
    [MustHavePermission(Permissions.ReportsView)]
    [ProducesResponseType(typeof(PaginationResponse<ReportSubmissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubmissions([FromQuery] GetSubmissionsRequest request)
    {
        var result = await Mediator.Send(new GetSubmissionsQuery(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Get a specific report submission by ID
    /// </summary>
    [HttpGet("{submissionId}")]
    [MustHavePermission(Permissions.ReportsView)]
    [ProducesResponseType(typeof(ReportSubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubmissionById(Guid submissionId)
    {
        var result = await Mediator.Send(new GetSubmissionByIdQuery(submissionId));

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Get report analytics and statistics
    /// </summary>
    [HttpGet("analytics")]
    [MustHavePermission(Permissions.ReportsView)]
    [ProducesResponseType(typeof(ReportAnalyticsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnalytics([FromQuery] Guid? templateId, [FromQuery] Guid? muqamId)
    {
        var result = await Mediator.Send(new GetAnalyticsQuery(templateId, muqamId));
        return Ok(result);
    }

    /// <summary>
    /// Get analytics for a specific submission window
    /// </summary>
    [HttpGet("analytics/window/{windowId}")]
    [MustHavePermission(Permissions.ReportsView)]
    [ProducesResponseType(typeof(SubmissionWindowAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubmissionWindowAnalytics(Guid windowId)
    {
        var result = await Mediator.Send(new GetSubmissionWindowAnalyticsQuery(windowId));

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Submit a new report
    /// </summary>
    [HttpPost]
    [MustHavePermission(Permissions.ReportsSubmit)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitReport([FromBody] SubmitReportRequest request)
    {
        var result = await Mediator.Send(new SubmitReportCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { id = result.Data, message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Save a report as draft
    /// </summary>
    [HttpPost("draft")]
    [MustHavePermission(Permissions.ReportsSubmit)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveDraft([FromBody] SaveDraftRequest request)
    {
        var result = await Mediator.Send(new SaveDraftCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { id = result.Data, message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Approve a report submission
    /// </summary>
    [HttpPost("{submissionId}/approve")]
    [MustHavePermission(Permissions.ReportsApprove)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveSubmission(Guid submissionId, [FromBody] ApproveSubmissionRequest request)
    {
        if (submissionId != request.SubmissionId)
        {
            return BadRequest(new { errors = new[] { "Submission ID mismatch" } });
        }

        var result = await Mediator.Send(new ApproveSubmissionCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Reject a report submission
    /// </summary>
    [HttpPost("{submissionId}/reject")]
    [MustHavePermission(Permissions.ReportsReject)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectSubmission(Guid submissionId, [FromBody] RejectSubmissionRequest request)
    {
        if (submissionId != request.SubmissionId)
        {
            return BadRequest(new { errors = new[] { "Submission ID mismatch" } });
        }

        var result = await Mediator.Send(new RejectSubmissionCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }
}
