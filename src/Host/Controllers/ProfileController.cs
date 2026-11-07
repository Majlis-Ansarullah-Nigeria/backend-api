using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Directory;
using ManagementApi.Application.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementApi.Host.Controllers;

/// <summary>The signed-in member's own profile and account. Every signed-in user may use it.</summary>
[Authorize]
public class ProfileController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(MyProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get() => Reply(await Mediator.Send(new GetMyProfileQuery()));

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request) =>
        Reply(await Mediator.Send(new ChangeMyPasswordCommand(request.CurrentPassword, request.NewPassword)));

    [HttpPost("email-change")]
    public async Task<IActionResult> ChangeEmail(ChangeMyEmailRequest request) =>
        Reply(await Mediator.Send(new ChangeMyEmailCommand(request.NewEmail, request.Password)));

    [HttpDelete("email-change")]
    public async Task<IActionResult> CancelEmailChange() => Reply(await Mediator.Send(new CancelMyEmailChangeCommand()));

    [HttpPost("report-mistake")]
    public async Task<IActionResult> ReportMistake(ReportMistakeRequest request) =>
        Reply(await Mediator.Send(new ReportRecordMistakeCommand(request.Field, request.Correction)));

    private IActionResult Reply(Result result) =>
        result.Succeeded ? Ok(new { message = result.Messages.FirstOrDefault() }) : BadRequest(new { errors = result.Messages });

    private IActionResult Reply<T>(Result<T> result) =>
        result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Messages });
}

/// <summary>Members directory. Public fields only.</summary>
[Authorize]
public class DirectoryController : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(DirectoryResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] Guid? zoneId,
        [FromQuery] Guid? dilaId,
        [FromQuery] Guid? muqamId,
        [FromQuery] string? position,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25)
    {
        var result = await Mediator.Send(new SearchDirectoryQuery(q, zoneId, dilaId, muqamId, position, pageNumber, pageSize));
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Messages });
    }

    [HttpGet("units")]
    [ProducesResponseType(typeof(DirectoryUnitsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Units()
    {
        var result = await Mediator.Send(new GetDirectoryUnitsQuery());
        return result.Succeeded ? Ok(result.Data) : BadRequest(new { errors = result.Messages });
    }
}
