using HRS.Api.Common;
using HRS.Application.Features.Review.Compiled;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class CompiledReportsEndpoints
{
    public static void MapCompiledReportsEndpoints(this IEndpointRouteBuilder app)
    {
        var compiled = app.MapGroup("/api/v1/compiled-reports").WithTags("CompiledReports").RequireAuthorization();

        compiled.MapGet("/", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetCompiledReportsQuery(user.GetUserId()), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetCompiledReports")
            .Produces<IReadOnlyList<CompiledReportListItem>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        compiled.MapGet("/{windowId:guid}", async (
                Guid windowId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetCompiledReportQuery(user.GetUserId(), windowId), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetCompiledReport")
            .Produces<CompiledReportView>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        compiled.MapPut("/{windowId:guid}/narrative", async (
                Guid windowId,
                [FromBody] UpdateCompiledNarrativeRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new UpdateCompiledNarrativeCommand(user.GetUserId(), windowId, request.Narrative), cancellationToken);
                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("UpdateCompiledNarrative")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        compiled.MapGet("/{windowId:guid}/pdf", (Guid windowId, System.Security.Claims.ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
                ExportAsync(windowId, CompiledReportFormat.Pdf, user, sender, cancellationToken))
            .WithName("GetCompiledReportPdf")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        compiled.MapGet("/{windowId:guid}/word", (Guid windowId, System.Security.Claims.ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
                ExportAsync(windowId, CompiledReportFormat.Word, user, sender, cancellationToken))
            .WithName("GetCompiledReportWord")
            .Produces(StatusCodes.Status200OK, contentType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ExportAsync(
        Guid windowId, CompiledReportFormat format, System.Security.Claims.ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ExportCompiledReportQuery(user.GetUserId(), windowId, format), cancellationToken);
        return result.IsSuccess
            ? Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName)
            : result.ToProblemDetails();
    }
}

public sealed record UpdateCompiledNarrativeRequest(string Narrative);
