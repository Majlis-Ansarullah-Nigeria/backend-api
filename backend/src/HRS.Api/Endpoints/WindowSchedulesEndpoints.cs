using HRS.Api.Common;
using HRS.Application.Features.Windows.WindowSchedules;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class WindowSchedulesEndpoints
{
    public static void MapWindowSchedulesEndpoints(this IEndpointRouteBuilder app)
    {
        var schedules = app.MapGroup("/api/v1/report-types/{reportTypeId:guid}/window-schedules")
            .WithTags("Windows")
            .RequireAuthorization();

        schedules.MapGet("/", async (
                Guid reportTypeId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetWindowSchedulesQuery(reportTypeId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetWindowSchedules")
            .Produces<IReadOnlyList<WindowScheduleSummary>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        schedules.MapGet("/{scheduleId:guid}", async (
                Guid reportTypeId,
                Guid scheduleId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetWindowScheduleQuery(reportTypeId, scheduleId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetWindowSchedule")
            .Produces<WindowScheduleSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        schedules.MapPost("/", async (
                Guid reportTypeId,
                [FromBody] CreateWindowScheduleRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateWindowScheduleCommand(
                    user.GetUserId(),
                    reportTypeId,
                    request.Frequency,
                    request.OpensDayOffset,
                    request.GraceDays,
                    request.ClosesDayOffset,
                    request.DueOffsets);

                var result = await sender.Send(command, cancellationToken);

                return result.IsFailure
                    ? result.ToProblemDetails()
                    : Results.Created($"/api/v1/report-types/{reportTypeId}/window-schedules/{result.Value.Schedule.Id}", result.Value);
            })
            .WithName("CreateWindowSchedule")
            .Produces<CreateWindowScheduleResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        schedules.MapPut("/{scheduleId:guid}", async (
                Guid reportTypeId,
                Guid scheduleId,
                [FromBody] UpdateWindowScheduleRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateWindowScheduleCommand(
                    user.GetUserId(),
                    reportTypeId,
                    scheduleId,
                    request.Frequency,
                    request.OpensDayOffset,
                    request.GraceDays,
                    request.ClosesDayOffset,
                    request.DueOffsets);

                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("UpdateWindowSchedule")
            .Produces<CreateWindowScheduleResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        schedules.MapPost("/{scheduleId:guid}/activate", async (
                Guid reportTypeId,
                Guid scheduleId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ActivateWindowScheduleCommand(user.GetUserId(), reportTypeId, scheduleId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("ActivateWindowSchedule")
            .Produces<CreateWindowScheduleResult>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        schedules.MapPost("/{scheduleId:guid}/deactivate", async (
                Guid reportTypeId,
                Guid scheduleId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new DeactivateWindowScheduleCommand(user.GetUserId(), reportTypeId, scheduleId), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("DeactivateWindowSchedule")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        schedules.MapPost("/{scheduleId:guid}/generate", async (
                Guid reportTypeId,
                Guid scheduleId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GenerateScheduledWindowsCommand(user.GetUserId(), reportTypeId, scheduleId), cancellationToken);

                return result.IsSuccess ? Results.Ok(new { windowsGenerated = result.Value }) : result.ToProblemDetails();
            })
            .WithName("GenerateScheduledWindows")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record CreateWindowScheduleRequest(
    ReportTypeFrequency Frequency,
    int OpensDayOffset,
    int GraceDays,
    int ClosesDayOffset,
    IReadOnlyList<ScheduleDueOffset> DueOffsets);

public sealed record UpdateWindowScheduleRequest(
    ReportTypeFrequency Frequency,
    int OpensDayOffset,
    int GraceDays,
    int ClosesDayOffset,
    IReadOnlyList<ScheduleDueOffset> DueOffsets);
