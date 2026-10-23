using System.Globalization;
using HRS.Api.Common;
using HRS.Application.Features.Audit;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var audit = app.MapGroup("/api/v1/audit-events").WithTags("Audit").RequireAuthorization();

        audit.MapGet("/", async (
                [FromQuery] string? action,
                [FromQuery] Guid? actorUserId,
                [FromQuery] Guid? unitId,
                [FromQuery] DateTimeOffset? from,
                [FromQuery] DateTimeOffset? to,
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var filter = new AuditFilter(action, actorUserId, unitId, from, to);
                var result = await sender.Send(new GetAuditEventsQuery(user.GetUserId(), filter, page ?? 1, pageSize ?? 50), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetAuditEvents")
            .Produces<AuditEventPage>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        audit.MapGet("/filters", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetAuditFilterOptionsQuery(user.GetUserId()), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetAuditFilterOptions")
            .Produces<AuditFilterOptions>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        audit.MapGet("/csv", async (
                [FromQuery] string? action,
                [FromQuery] Guid? actorUserId,
                [FromQuery] Guid? unitId,
                [FromQuery] DateTimeOffset? from,
                [FromQuery] DateTimeOffset? to,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var filter = new AuditFilter(action, actorUserId, unitId, from, to);
                var result = await sender.Send(new ExportAuditEventsCsvQuery(user.GetUserId(), filter), cancellationToken);
                if (result.IsFailure)
                {
                    return result.ToProblemDetails();
                }

                // The export is itself an audited download (FR-AUD-001).
                await sender.Send(
                    new RecordDownloadCommand(user.GetUserId(), DownloadKind.AuditLog, Guid.Empty, $"{result.Value.RowCount.ToString(CultureInfo.InvariantCulture)} rows"),
                    cancellationToken);
                return Results.File(result.Value.Content, "text/csv", result.Value.FileName);
            })
            .WithName("ExportAuditEventsCsv")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
