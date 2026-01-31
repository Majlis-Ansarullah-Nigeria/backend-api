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
public class NotificationsController : BaseApiController
{
    /// <summary>
    /// Get notifications for the current user with filtering and pagination
    /// </summary>
    [HttpGet]
    [MustHavePermission(Permissions.ReportsView)]
    [ProducesResponseType(typeof(PaginationResponse<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isRead = null,
        [FromQuery] string? type = null)
    {
        var result = await Mediator.Send(new GetUserNotificationsQuery(pageNumber, pageSize, isRead, type));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Get unread notification count for the current user
    /// </summary>
    [HttpGet("stats")]
    [MustHavePermission(Permissions.ReportsView)]
    [ProducesResponseType(typeof(NotificationStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotificationStats()
    {
        var result = await Mediator.Send(new GetUnreadNotificationCountQuery());

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(result.Data);
    }

    /// <summary>
    /// Mark a notification as read
    /// </summary>
    [HttpPost("{notificationId}/read")]
    [MustHavePermission(Permissions.ReportsView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid notificationId)
    {
        var result = await Mediator.Send(new MarkNotificationAsReadCommand(notificationId));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Mark all notifications as read for the current user
    /// </summary>
    [HttpPost("read-all")]
    [MustHavePermission(Permissions.ReportsView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var result = await Mediator.Send(new MarkAllNotificationsAsReadCommand());

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }

    /// <summary>
    /// Create a notification (internal use - typically triggered by system events)
    /// </summary>
    [HttpPost]
    [MustHavePermission(Permissions.ReportTemplatesManage)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationRequest request)
    {
        var result = await Mediator.Send(new CreateNotificationCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { id = result.Data, message = result.Messages.FirstOrDefault() });
    }
}
