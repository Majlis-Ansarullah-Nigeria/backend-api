using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Commands;
using ManagementApi.Application.Reports.DTOs;
using ManagementApi.Application.Reports.Queries;
using ManagementApi.Infrastructure.Authorization;
using ManagementApi.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementApi.Host.Controllers;

public record FileUploadRequest
{
    public IFormFile File { get; init; } = default!;
    public Guid QuestionId { get; init; }
    public string? Description { get; init; }
}

[Authorize]
public class ReportSubmissionsController : BaseApiController
{
    // Only the National Secretariat reviews reports (acknowledge, return, reopen).
    private const string SecretariatRoles = Roles.NationalAdmin + "," + Roles.SuperAdmin;

    private IActionResult ToActionResult(Result result)
    {
        if (result.Succeeded)
        {
            return Ok(new { message = result.Messages.FirstOrDefault() });
        }

        if (result.Messages.Any(m => m.StartsWith("FORBIDDEN")))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { errors = result.Messages });
        }

        if (result.Messages.Any(m => m == "Submission not found"))
        {
            return NotFound(new { errors = result.Messages });
        }

        return BadRequest(new { errors = result.Messages });
    }

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
    /// Get overdue submissions - windows that have passed their deadline with missing submissions
    /// </summary>
    [HttpGet("overdue")]
    [MustHavePermission(Permissions.ReportsView)]
    [ProducesResponseType(typeof(List<OverdueSubmissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverdueSubmissions()
    {
        var result = await Mediator.Send(new GetOverdueSubmissionsQuery());

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
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
    /// Withdraw the current unit's submitted report back to draft (only before the Secretariat opens it, while the window is open)
    /// </summary>
    [HttpPost("{submissionId}/withdraw")]
    [MustHavePermission(Permissions.ReportsSubmit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> WithdrawSubmission(Guid submissionId)
    {
        var result = await Mediator.Send(new WithdrawSubmissionCommand(submissionId));
        return ToActionResult(result);
    }

    /// <summary>
    /// Mark a submitted report as In Review (National Secretariat opened it)
    /// </summary>
    [HttpPost("{submissionId}/start-review")]
    [Authorize(Roles = SecretariatRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> StartReview(Guid submissionId)
    {
        var result = await Mediator.Send(new StartReviewCommand(submissionId));
        return ToActionResult(result);
    }

    /// <summary>
    /// Acknowledge a report (final). National Secretariat only.
    /// </summary>
    [HttpPost("{submissionId}/acknowledge")]
    [Authorize(Roles = SecretariatRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcknowledgeSubmission(Guid submissionId, [FromBody] AcknowledgeSubmissionRequest request)
    {
        var result = await Mediator.Send(new AcknowledgeSubmissionCommand(request with { SubmissionId = submissionId }));
        return ToActionResult(result);
    }

    /// <summary>
    /// Return a report to the unit for correction, with a reason. National Secretariat only.
    /// </summary>
    [HttpPost("{submissionId}/return")]
    [Authorize(Roles = SecretariatRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReturnSubmission(Guid submissionId, [FromBody] ReturnSubmissionRequest request)
    {
        var result = await Mediator.Send(new ReturnSubmissionCommand(request with { SubmissionId = submissionId }));
        return ToActionResult(result);
    }

    /// <summary>
    /// Reopen an acknowledged report for further review, with a reason. National Secretariat only.
    /// </summary>
    [HttpPost("{submissionId}/reopen")]
    [Authorize(Roles = SecretariatRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReopenSubmission(Guid submissionId, [FromBody] ReopenSubmissionRequest request)
    {
        var result = await Mediator.Send(new ReopenSubmissionCommand(request with { SubmissionId = submissionId }));
        return ToActionResult(result);
    }

    /// <summary>
    /// Upload a file attachment to a submission
    /// </summary>
    [HttpPost("{submissionId}/files")]
    [MustHavePermission(Permissions.ReportsSubmit)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadFile(Guid submissionId, [FromForm] FileUploadRequest formRequest)
    {
        if (formRequest.File == null || formRequest.File.Length == 0)
        {
            return BadRequest(new { errors = new[] { "File is required" } });
        }

        // Read file data
        byte[] fileData;
        using (var memoryStream = new MemoryStream())
        {
            await formRequest.File.CopyToAsync(memoryStream);
            fileData = memoryStream.ToArray();
        }

        var request = new UploadFileRequest
        {
            SubmissionId = submissionId,
            QuestionId = formRequest.QuestionId,
            FileName = formRequest.File.FileName,
            ContentType = formRequest.File.ContentType,
            FileData = fileData,
            Description = formRequest.Description
        };

        var result = await Mediator.Send(new UploadFileCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { id = result.Data, message = "File uploaded successfully" });
    }

    /// <summary>
    /// Get all file attachments for a submission
    /// </summary>
    [HttpGet("{submissionId}/files")]
    [MustHavePermission(Permissions.ReportsView)]
    [ProducesResponseType(typeof(List<FileAttachmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFileAttachments(Guid submissionId)
    {
        var result = await Mediator.Send(new GetFileAttachmentsQuery(submissionId));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Download a file attachment
    /// </summary>
    [HttpGet("files/{fileAttachmentId}")]
    [MustHavePermission(Permissions.ReportsView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadFile(Guid fileAttachmentId)
    {
        var result = await Mediator.Send(new DownloadFileQuery(fileAttachmentId));

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Messages });
        }

        return File(result.Data!.FileData, result.Data.ContentType, result.Data.FileName);
    }

    /// <summary>
    /// Delete a file attachment
    /// </summary>
    [HttpDelete("files/{fileAttachmentId}")]
    [MustHavePermission(Permissions.ReportsSubmit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteFile(Guid fileAttachmentId)
    {
        var result = await Mediator.Send(new DeleteFileCommand(fileAttachmentId));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = "File deleted successfully" });
    }

    /// <summary>
    /// Acknowledge several reports at once. National Secretariat only.
    /// </summary>
    [HttpPost("bulk/acknowledge")]
    [Authorize(Roles = SecretariatRoles)]
    [ProducesResponseType(typeof(BulkReviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkAcknowledge([FromBody] BulkAcknowledgeRequest request)
    {
        var result = await Mediator.Send(new BulkAcknowledgeCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { data = result.Data, message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Return several reports for correction at once, with one reason. National Secretariat only.
    /// </summary>
    [HttpPost("bulk/return")]
    [Authorize(Roles = SecretariatRoles)]
    [ProducesResponseType(typeof(BulkReviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkReturn([FromBody] BulkReturnRequest request)
    {
        var result = await Mediator.Send(new BulkReturnCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { data = result.Data, message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Flag a submission for National attention
    /// </summary>
    [HttpPost("{submissionId}/flag")]
    [MustHavePermission(Permissions.ReportsView)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> FlagSubmission(Guid submissionId, [FromBody] FlagSubmissionRequest request)
    {
        if (submissionId != request.SubmissionId)
        {
            return BadRequest(new { errors = new[] { "Submission ID mismatch" } });
        }

        var result = await Mediator.Send(new FlagSubmissionCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { id = result.Data, message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Get all flagged submissions
    /// </summary>
    [HttpGet("flagged")]
    [MustHavePermission(Permissions.ReportsView)]
    [ProducesResponseType(typeof(PaginationResponse<SubmissionFlagDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFlaggedSubmissions([FromQuery] bool onlyActive = true, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetFlaggedSubmissionsQuery(onlyActive, pageNumber, pageSize));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Resolve/unflag a flagged submission
    /// </summary>
    [HttpPost("flags/{flagId}/resolve")]
    [MustHavePermission(Permissions.ReportsApprove)]
    [Authorize(Roles = SecretariatRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnflagSubmission(Guid flagId, [FromBody] UnflagSubmissionRequest request)
    {
        if (flagId != request.FlagId)
        {
            return BadRequest(new { errors = new[] { "Flag ID mismatch" } });
        }

        var result = await Mediator.Send(new UnflagSubmissionCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Add a comment to a submission (US-25: Standalone Comments System)
    /// </summary>
    [HttpPost("{submissionId}/comments")]
    [MustHavePermission("Permissions.Reports.ViewSubmissions")]
    public async Task<IActionResult> AddComment(Guid submissionId, [FromBody] AddCommentRequest request)
    {
        // Ensure the submissionId in the route matches the request
        if (request.SubmissionId != submissionId)
        {
            return BadRequest("Submission ID in route does not match request");
        }

        var result = await Mediator.Send(new AddCommentCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get all comments for a submission with optional threading (US-25: Standalone Comments System)
    /// </summary>
    [HttpGet("{submissionId}/comments")]
    [MustHavePermission("Permissions.Reports.ViewSubmissions")]
    public async Task<IActionResult> GetComments(
        Guid submissionId,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] bool includeReplies = true,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await Mediator.Send(new GetCommentsQuery(
            submissionId,
            includeDeleted,
            includeReplies,
            pageNumber,
            pageSize));

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Update a comment (US-25: Standalone Comments System)
    /// </summary>
    [HttpPut("comments/{commentId}")]
    [MustHavePermission("Permissions.Reports.ViewSubmissions")]
    public async Task<IActionResult> UpdateComment(Guid commentId, [FromBody] UpdateCommentRequest request)
    {
        // Ensure the commentId in the route matches the request
        if (request.CommentId != commentId)
        {
            return BadRequest("Comment ID in route does not match request");
        }

        var result = await Mediator.Send(new UpdateCommentCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Delete a comment (soft delete) (US-25: Standalone Comments System)
    /// </summary>
    [HttpDelete("comments/{commentId}")]
    [MustHavePermission("Permissions.Reports.ViewSubmissions")]
    public async Task<IActionResult> DeleteComment(Guid commentId)
    {
        var request = new DeleteCommentRequest { CommentId = commentId };
        var result = await Mediator.Send(new DeleteCommentCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
