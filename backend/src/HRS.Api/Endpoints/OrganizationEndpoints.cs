using HRS.Api.Common;
using HRS.Application.Features.Organization.Levels;
using HRS.Application.Features.Organization.Units;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var units = app.MapGroup("/api/v1/units").WithTags("Organization").RequireAuthorization();

        units.MapGet("/tree", async (
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] bool includeArchived = false) =>
            {
                var result = await sender.Send(new GetOrganizationTreeQuery(includeArchived), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetOrganizationTree")
            .Produces<IReadOnlyList<UnitTreeNode>>();

        app.MapGet("/api/v1/unit-levels", async (
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetUnitLevelsQuery(), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetUnitLevels")
            .WithTags("Organization")
            .RequireAuthorization()
            .Produces<IReadOnlyList<UnitLevelSummary>>();

        units.MapPost("/", async (
                [FromBody] CreateUnitRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateUnitCommand(
                    user.GetUserId(),
                    request.Code,
                    request.Name,
                    request.LevelId,
                    request.ParentId,
                    request.Address,
                    request.ContactPhone,
                    request.ContactEmail,
                    request.Latitude,
                    request.Longitude);

                var result = await sender.Send(command, cancellationToken);

                return result.IsFailure
                    ? result.ToProblemDetails()
                    : Results.Created($"/api/v1/units/{result.Value.Id}", result.Value);
            })
            .WithName("CreateUnit")
            .Produces<UnitSummary>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        units.MapGet("/", async (
                [FromQuery] Guid? parentId,
                [FromQuery] Guid? levelId,
                [FromQuery] string? q,
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] bool includeArchived = false) =>
            {
                var query = new GetUnitsQuery(parentId, levelId, q, includeArchived);
                var result = await sender.Send(query, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetUnits")
            .Produces<IReadOnlyList<UnitSummary>>();

        units.MapGet("/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetUnitQuery(id), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetUnit")
            .Produces<UnitSummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        units.MapPut("/{id:guid}", async (
                Guid id,
                [FromBody] UpdateUnitRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateUnitCommand(
                    user.GetUserId(),
                    id,
                    request.Name,
                    request.Address,
                    request.ContactPhone,
                    request.ContactEmail,
                    request.Latitude,
                    request.Longitude);

                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("UpdateUnit")
            .Produces<UnitSummary>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        units.MapDelete("/{id:guid}", async (
                Guid id,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ArchiveUnitCommand(user.GetUserId(), id), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("ArchiveUnit")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        units.MapPost("/{id:guid}/restore", async (
                Guid id,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new RestoreUnitCommand(user.GetUserId(), id), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("RestoreUnit")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record CreateUnitRequest(
    string Code,
    string Name,
    Guid LevelId,
    Guid ParentId,
    string? Address,
    string? ContactPhone,
    string? ContactEmail,
    double? Latitude,
    double? Longitude);

public sealed record UpdateUnitRequest(
    string Name,
    string? Address,
    string? ContactPhone,
    string? ContactEmail,
    double? Latitude,
    double? Longitude);
