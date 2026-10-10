using HRS.Api.Common;
using HRS.Application.Features.Observer.ChildReport;
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

        var childReports = app.MapGroup("/api/v1/units/{unitId:guid}/reports/{reportId:guid}")
            .WithTags("Observer")
            .RequireAuthorization();

        childReports.MapGet("/", async (
                Guid unitId,
                Guid reportId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetChildReportQuery(user.GetUserId(), unitId, reportId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetChildReport")
            .Produces<ChildReportView>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        childReports.MapGet("/pdf", async (
                Guid unitId,
                Guid reportId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetChildReportPdfQuery(user.GetUserId(), unitId, reportId), cancellationToken);

                return result.IsSuccess
                    ? Results.File(result.Value, "application/pdf", "child-report.pdf")
                    : result.ToProblemDetails();
            })
            .WithName("GetChildReportPdf")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
