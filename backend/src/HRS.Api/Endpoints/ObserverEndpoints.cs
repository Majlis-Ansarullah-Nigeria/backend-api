using HRS.Api.Common;
using HRS.Application.Features.Observer.ComplianceGrid;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class ObserverEndpoints
{
    public static void MapObserverEndpoints(this IEndpointRouteBuilder app)
    {
        var units = app.MapGroup("/api/v1/units").WithTags("Observer").RequireAuthorization();

        units.MapGet("/compliance-grid", async (
                [FromQuery] Guid? parentUnitId,
                [FromQuery] Guid? reportTypeId,
                [FromQuery] int? periods,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetComplianceGridQuery(user.GetUserId(), parentUnitId, reportTypeId, periods ?? 0);
                var result = await sender.Send(query, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetComplianceGrid")
            .Produces<ComplianceGridResult>()
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
