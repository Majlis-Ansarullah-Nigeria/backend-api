using ManagementApi.Application.Reports.ReportTypes;
using ManagementApi.Infrastructure.Authorization;
using ManagementApi.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementApi.Host.Controllers;

/// <summary>
/// Report types (e.g. General Monthly Report, code GMR). The code starts every reference number.
/// Managed by the National Secretariat.
/// </summary>
[Authorize]
public class ReportTypesController : BaseApiController
{
    private const string SecretariatRoles = Roles.NationalAdmin + "," + Roles.SuperAdmin;

    [HttpGet]
    [MustHavePermission(Permissions.ReportsView)]
    [ProducesResponseType(typeof(List<ReportTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReportTypes()
    {
        return Ok(await Mediator.Send(new GetReportTypesQuery()));
    }

    [HttpPost]
    [Authorize(Roles = SecretariatRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReportType([FromBody] SaveReportTypeRequest request)
    {
        var result = await Mediator.Send(new CreateReportTypeCommand(request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { id = result.Data, message = result.Messages.FirstOrDefault() });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = SecretariatRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateReportType(Guid id, [FromBody] SaveReportTypeRequest request)
    {
        var result = await Mediator.Send(new UpdateReportTypeCommand(id, request));

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Messages });
        }

        return Ok(new { message = result.Messages.FirstOrDefault() });
    }
}
