using HRS.Api.Common;
using HRS.Application.Features.History;
using HRS.Domain.History;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class ExportsEndpoints
{
    public static void MapExportsEndpoints(this IEndpointRouteBuilder app)
    {
        var exports = app.MapGroup("/api/v1/exports").WithTags("Exports").RequireAuthorization();

        exports.MapGet("/", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetMyExportsQuery(user.GetUserId()), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetMyExports")
            .Produces<IReadOnlyList<ExportSummary>>();

        exports.MapGet("/{id:guid}", async (
                Guid id,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetExportQuery(user.GetUserId(), id), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetExport")
            .Produces<ExportSummary>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        exports.MapGet("/{id:guid}/download", async (
                Guid id,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetExportDownloadUrlQuery(user.GetUserId(), id), cancellationToken);
                return result.IsSuccess ? Results.Ok(new ExportDownloadUrlResponse(result.Value)) : result.ToProblemDetails();
            })
            .WithName("GetExportDownloadUrl")
            .Produces<ExportDownloadUrlResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var secretariatExports = app.MapGroup("/api/v1/secretariat/exports").WithTags("Exports").RequireAuthorization();

        secretariatExports.MapPost("/", async (
                [FromBody] CreateOrganizationExportRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateOrganizationExportCommand(
                    user.GetUserId(), request.ReportTypeId, request.FromWindowId, request.ToWindowId, request.Status, request.Format);
                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("CreateOrganizationExport")
            .Produces<ExportSummary>()
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}

public sealed record CreateOrganizationExportRequest(
    Guid? ReportTypeId, Guid? FromWindowId, Guid? ToWindowId, int? Status, BulkExportFormat Format);

public sealed record ExportDownloadUrlResponse(string Url);
