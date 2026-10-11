using HRS.Api.Common;
using HRS.Application.Features.Review.Inbox;
using HRS.Application.Features.Review.SavedViews;
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
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetInboxQuery(user.GetUserId(), reportTypeId, windowId, unitLevel, zoneUnitId, status, isLate, isFlagged);
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
