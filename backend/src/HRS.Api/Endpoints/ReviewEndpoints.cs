using HRS.Api.Common;
using HRS.Application.Features.Review.Inbox;
using HRS.Application.Features.Review.SavedViews;
using HRS.Application.Features.Review.Workload;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var secretariat = app.MapGroup("/api/v1/secretariat").WithTags("Review").RequireAuthorization();

        secretariat.MapGet("/inbox", async (
                [FromQuery] Guid? reportTypeId,
                [FromQuery] Guid? windowId,
                [FromQuery] UnitLevelCode? unitLevel,
                [FromQuery] Guid? zoneUnitId,
                [FromQuery] ReportInstanceStatus? status,
                [FromQuery] bool? isLate,
                [FromQuery] bool? isFlagged,
                [FromQuery] Guid? assignedToUserId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetInboxQuery(
                    user.GetUserId(), reportTypeId, windowId, unitLevel, zoneUnitId, status, isLate, isFlagged, assignedToUserId);
                var result = await sender.Send(query, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetInbox")
            .Produces<IReadOnlyList<InboxItem>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        secretariat.MapGet("/inbox/{id:guid}", async (
                Guid id,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetInboxReportDetailQuery(user.GetUserId(), id), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetInboxReportDetail")
            .Produces<InboxReportDetail>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        secretariat.MapPost("/inbox/{id:guid}/open", async (
                Guid id,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new OpenReportCommand(user.GetUserId(), id), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("OpenInboxReport")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        secretariat.MapPost("/inbox/{id:guid}/acknowledge", async (
                Guid id,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new AcknowledgeReportCommand(user.GetUserId(), id), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("AcknowledgeInboxReport")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        secretariat.MapPost("/inbox/{id:guid}/return", async (
                Guid id,
                [FromBody] ReturnInboxReportRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ReturnReportCommand(user.GetUserId(), id, request.Comment), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("ReturnInboxReport")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        secretariat.MapPut("/inbox/{id:guid}/flag", async (
                Guid id,
                [FromBody] SetInboxReportFlaggedRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new SetReportFlaggedCommand(user.GetUserId(), id, request.IsFlagged), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("SetInboxReportFlagged")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        secretariat.MapPut("/inbox/{id:guid}/assign", async (
                Guid id,
                [FromBody] AssignInboxReportRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new AssignReportCommand(user.GetUserId(), id, request.AssignedToUserId), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("AssignInboxReport")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        secretariat.MapGet("/workload", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetWorkloadQuery(user.GetUserId()), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetWorkload")
            .Produces<WorkloadView>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        secretariat.MapGet("/saved-views", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetSavedViewsQuery(user.GetUserId()), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetSavedViews")
            .Produces<IReadOnlyList<SavedViewSummary>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        secretariat.MapPost("/saved-views", async (
                [FromBody] CreateSavedViewRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateSavedViewCommand(user.GetUserId(), request.Name, request.Filter);
                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("CreateSavedView")
            .Produces<SavedViewSummary>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        secretariat.MapDelete("/saved-views/{id:guid}", async (
                Guid id,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new DeleteSavedViewCommand(user.GetUserId(), id), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("DeleteSavedView")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record CreateSavedViewRequest(string Name, InboxFilter Filter);

public sealed record ReturnInboxReportRequest(string Comment);

public sealed record SetInboxReportFlaggedRequest(bool IsFlagged);

public sealed record AssignInboxReportRequest(Guid? AssignedToUserId);
