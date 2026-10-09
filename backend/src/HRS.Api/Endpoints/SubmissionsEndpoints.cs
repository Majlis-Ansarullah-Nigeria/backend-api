using HRS.Api.Common;
using HRS.Application.Features.Submissions.Attachments;
using HRS.Application.Features.Submissions.MyReports;
using HRS.Application.Features.Submissions.ReportInstances;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class SubmissionsEndpoints
{
    public static void MapSubmissionsEndpoints(this IEndpointRouteBuilder app)
    {
        var myReports = app.MapGroup("/api/v1/me/reports").WithTags("Submissions").RequireAuthorization();

        myReports.MapGet("/", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetMyReportsQuery(user.GetUserId()), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetMyReports")
            .Produces<IReadOnlyList<MyReportSummary>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        var reports = app.MapGroup("/api/v1/reports/{id:guid}").WithTags("Submissions").RequireAuthorization();

        reports.MapGet("/", async (
                Guid id,
                [FromQuery(Name = "unit")] Guid unitId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetReportInstanceForEditingQuery(user.GetUserId(), id, unitId), cancellationToken);

                if (result.IsFailure)
                {
                    return result.ToProblemDetails();
                }

                if (result.Value.RowVersion is { } rowVersion)
                {
                    httpContext.Response.Headers.ETag = $"\"{rowVersion}\"";
                }

                return Results.Ok(result.Value);
            })
            .WithName("GetReportInstanceForEditing")
            .Produces<ReportInstanceForEditing>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        reports.MapPut("/", async (
                Guid id,
                [FromQuery(Name = "unit")] Guid unitId,
                [FromBody] SaveReportDraftRequest request,
                [FromHeader(Name = "If-Match")] string? ifMatch,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var command = new SaveReportDraftCommand(user.GetUserId(), id, unitId, request.Answers, ifMatch);
                var result = await sender.Send(command, cancellationToken);

                if (result.IsFailure)
                {
                    return result.ToProblemDetails();
                }

                httpContext.Response.Headers.ETag = $"\"{result.Value.RowVersion}\"";
                return Results.Ok(result.Value);
            })
            .WithName("SaveReportDraft")
            .Produces<SaveReportDraftResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var attachments = app.MapGroup("/api/v1/reports/{id:guid}/attachments").WithTags("Submissions").RequireAuthorization();

        attachments.MapPost("/presign", async (
                Guid id,
                [FromQuery(Name = "unit")] Guid unitId,
                [FromBody] PresignAttachmentRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new PresignAttachmentUploadCommand(
                    user.GetUserId(), id, unitId, request.FieldKey, request.FileName, request.ContentType, request.SizeBytes);
                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("PresignAttachmentUpload")
            .Produces<PresignedAttachmentUpload>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        attachments.MapPost("/{attachmentId:guid}/complete", async (
                Guid id,
                Guid attachmentId,
                [FromQuery(Name = "unit")] Guid unitId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CompleteAttachmentUploadCommand(user.GetUserId(), id, unitId, attachmentId);
                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("CompleteAttachmentUpload")
            .Produces<AttachmentSummary>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        attachments.MapDelete("/{attachmentId:guid}", async (
                Guid id,
                Guid attachmentId,
                [FromQuery(Name = "unit")] Guid unitId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new DeleteAttachmentCommand(user.GetUserId(), id, unitId, attachmentId);
                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("DeleteAttachment")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

public sealed record SaveReportDraftRequest(Dictionary<string, object?> Answers);

public sealed record PresignAttachmentRequest(string FieldKey, string FileName, string ContentType, long SizeBytes);
