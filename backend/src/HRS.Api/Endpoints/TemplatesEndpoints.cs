using HRS.Api.Common;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Domain.Organization;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class TemplatesEndpoints
{
    public static void MapTemplatesEndpoints(this IEndpointRouteBuilder app)
    {
        var reportTypes = app.MapGroup("/api/v1/report-types").WithTags("Templates").RequireAuthorization();

        reportTypes.MapGet("/", async (
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] bool includeArchived = false) =>
            {
                var result = await sender.Send(new GetReportTypesQuery(includeArchived), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetReportTypes")
            .Produces<IReadOnlyList<ReportTypeSummary>>();

        reportTypes.MapGet("/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetReportTypeQuery(id), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetReportType")
            .Produces<ReportTypeSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        reportTypes.MapPost("/", async (
                [FromBody] CreateReportTypeRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateReportTypeCommand(
                    user.GetUserId(),
                    request.Name,
                    request.Description,
                    request.Category,
                    request.Frequency,
                    request.ApplicableLevels,
                    request.RecipientLevel,
                    request.ObserverDepth,
                    request.AllowAttachments);

                var result = await sender.Send(command, cancellationToken);

                return result.IsFailure
                    ? result.ToProblemDetails()
                    : Results.Created($"/api/v1/report-types/{result.Value.Id}", result.Value);
            })
            .WithName("CreateReportType")
            .Produces<ReportTypeSummary>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        reportTypes.MapPut("/{id:guid}", async (
                Guid id,
                [FromBody] UpdateReportTypeRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateReportTypeCommand(
                    user.GetUserId(),
                    id,
                    request.Name,
                    request.Description,
                    request.Category,
                    request.Frequency,
                    request.ApplicableLevels,
                    request.RecipientLevel,
                    request.ObserverDepth,
                    request.AllowAttachments);

                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("UpdateReportType")
            .Produces<ReportTypeSummary>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        reportTypes.MapPost("/{id:guid}/archive", async (
                Guid id,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ArchiveReportTypeCommand(user.GetUserId(), id), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("ArchiveReportType")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        reportTypes.MapPost("/{id:guid}/restore", async (
                Guid id,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new RestoreReportTypeCommand(user.GetUserId(), id), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("RestoreReportType")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record CreateReportTypeRequest(
    string Name,
    string? Description,
    ReportTypeCategory Category,
    ReportTypeFrequency Frequency,
    IReadOnlyList<UnitLevelCode> ApplicableLevels,
    UnitLevelCode RecipientLevel,
    int ObserverDepth,
    bool AllowAttachments);

public sealed record UpdateReportTypeRequest(
    string Name,
    string? Description,
    ReportTypeCategory Category,
    ReportTypeFrequency Frequency,
    IReadOnlyList<UnitLevelCode> ApplicableLevels,
    UnitLevelCode RecipientLevel,
    int ObserverDepth,
    bool AllowAttachments);
