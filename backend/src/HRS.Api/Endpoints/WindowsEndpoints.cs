using HRS.Api.Common;
using HRS.Application.Features.Windows.SubmissionWindows;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class WindowsEndpoints
{
    public static void MapWindowsEndpoints(this IEndpointRouteBuilder app)
    {
        var windows = app.MapGroup("/api/v1/report-types/{reportTypeId:guid}/windows")
            .WithTags("Windows")
            .RequireAuthorization();

        windows.MapGet("/", async (
                Guid reportTypeId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetSubmissionWindowsQuery(reportTypeId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetSubmissionWindows")
            .Produces<IReadOnlyList<SubmissionWindowSummary>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        windows.MapGet("/{windowId:guid}", async (
                Guid reportTypeId,
                Guid windowId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetSubmissionWindowQuery(reportTypeId, windowId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetSubmissionWindow")
            .Produces<SubmissionWindowSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        windows.MapPost("/", async (
                Guid reportTypeId,
                [FromBody] CreateSubmissionWindowRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateSubmissionWindowCommand(
                    user.GetUserId(),
                    reportTypeId,
                    request.PeriodLabel,
                    request.OpensAt,
                    request.GraceDays,
                    request.ClosesAt,
                    request.DueDates);

                var result = await sender.Send(command, cancellationToken);

                return result.IsFailure
                    ? result.ToProblemDetails()
                    : Results.Created($"/api/v1/report-types/{reportTypeId}/windows/{result.Value.Window.Id}", result.Value);
            })
            .WithName("CreateSubmissionWindow")
            .Produces<CreateSubmissionWindowResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        windows.MapPut("/{windowId:guid}", async (
                Guid reportTypeId,
                Guid windowId,
                [FromBody] UpdateSubmissionWindowRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateSubmissionWindowCommand(
                    user.GetUserId(),
                    reportTypeId,
                    windowId,
                    request.PeriodLabel,
                    request.OpensAt,
                    request.GraceDays,
                    request.ClosesAt,
                    request.DueDates);

                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("UpdateSubmissionWindow")
            .Produces<CreateSubmissionWindowResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record CreateSubmissionWindowRequest(
    string PeriodLabel,
    DateTimeOffset OpensAt,
    int GraceDays,
    DateTimeOffset ClosesAt,
    IReadOnlyList<WindowDueDate> DueDates);

public sealed record UpdateSubmissionWindowRequest(
    string PeriodLabel,
    DateTimeOffset OpensAt,
    int GraceDays,
    DateTimeOffset ClosesAt,
    IReadOnlyList<WindowDueDate> DueDates);
