using HRS.Api.Common;
using HRS.Application.Features.Observer.ChildReport;
using HRS.Application.Features.Observer.ComplianceGrid;
using HRS.Application.Features.Observer.Nudge;
using HRS.Application.Features.Observer.PrivateNotes;
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

        units.MapPost("/{unitId:guid}/nudge", async (
                Guid unitId,
                [FromBody] SendNudgeRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new SendNudgeCommand(user.GetUserId(), unitId, request.WindowId), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("SendNudge")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

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

        childReports.MapGet("/private-notes", async (
                Guid unitId,
                Guid reportId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetPrivateNotesQuery(user.GetUserId(), unitId, reportId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetPrivateNotes")
            .Produces<IReadOnlyList<PrivateNoteView>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        childReports.MapPost("/private-notes", async (
                Guid unitId,
                Guid reportId,
                [FromBody] AddPrivateNoteRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new AddPrivateNoteCommand(user.GetUserId(), unitId, reportId, request.Body), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("AddPrivateNote")
            .Produces<PrivateNoteView>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

public sealed record SendNudgeRequest(Guid WindowId);

public sealed record AddPrivateNoteRequest(string Body);
