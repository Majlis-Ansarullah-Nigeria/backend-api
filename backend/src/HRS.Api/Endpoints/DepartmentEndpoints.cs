using HRS.Api.Common;
using HRS.Application.Features.Review.Department;
using HRS.Domain.Organization;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class DepartmentEndpoints
{
    public static void MapDepartmentEndpoints(this IEndpointRouteBuilder app)
    {
        var department = app.MapGroup("/api/v1/department").WithTags("Department").RequireAuthorization();

        department.MapGet("/reports", async (
                [FromQuery] Guid? reportTypeId,
                [FromQuery] Guid? windowId,
                [FromQuery] UnitLevelCode? unitLevel,
                [FromQuery] Guid? zoneUnitId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetDepartmentReportsQuery(user.GetUserId(), reportTypeId, windowId, unitLevel, zoneUnitId);
                var result = await sender.Send(query, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetDepartmentReports")
            .Produces<IReadOnlyList<DepartmentReportRow>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
