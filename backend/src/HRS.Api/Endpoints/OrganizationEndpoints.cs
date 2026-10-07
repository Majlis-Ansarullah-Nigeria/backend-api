using HRS.Api.Common;
using HRS.Application.Features.Organization.BulkImport;
using HRS.Application.Features.Organization.Levels;
using HRS.Application.Features.Organization.RoleAssignments;
using HRS.Application.Features.Organization.Units;
using HRS.Domain.Access;
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

        units.MapGet("/bulk-import/template", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GenerateBulkImportTemplateQuery(user.GetUserId()), cancellationToken);

                return result.IsSuccess
                    ? Results.File(
                        result.Value,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "units_and_heads_template.xlsx")
                    : result.ToProblemDetails();
            })
            .WithName("GetBulkImportTemplate")
            .ProducesProblem(StatusCodes.Status403Forbidden);

        units.MapPost("/bulk-import/preview", async (
                IFormFile file,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var content = await ReadAllBytesAsync(file, cancellationToken);
                var result = await sender.Send(
                    new PreviewBulkImportQuery(user.GetUserId(), content, file.FileName), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("PreviewBulkImport")
            .DisableAntiforgery()
            .Produces<BulkImportSummary>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        units.MapPost("/bulk-import/commit", async (
                IFormFile file,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var content = await ReadAllBytesAsync(file, cancellationToken);
                var result = await sender.Send(
                    new CommitBulkImportCommand(user.GetUserId(), content, file.FileName), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("CommitBulkImport")
            .DisableAntiforgery()
            .Produces<BulkImportCommitResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

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

        units.MapPost("/{id:guid}/move", async (
                Guid id,
                [FromBody] MoveUnitRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new MoveUnitCommand(user.GetUserId(), id, request.NewParentId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("MoveUnit")
            .Produces<UnitSummary>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        units.MapPost("/{id:guid}/assignments", async (
                Guid id,
                [FromBody] CreateRoleAssignmentRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateRoleAssignmentCommand(
                    user.GetUserId(),
                    id,
                    request.Email,
                    request.Name,
                    request.Role,
                    request.DelegateScope,
                    request.StartsAt,
                    request.EndsAt);

                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("CreateRoleAssignment")
            .Produces<CreateRoleAssignmentResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<byte[]> ReadAllBytesAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        return stream.ToArray();
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

public sealed record MoveUnitRequest(Guid NewParentId);

public sealed record UpdateUnitRequest(
    string Name,
    string? Address,
    string? ContactPhone,
    string? ContactEmail,
    double? Latitude,
    double? Longitude);

public sealed record CreateRoleAssignmentRequest(
    string Email,
    string Name,
    RoleName Role,
    DelegateScope? DelegateScope,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt);
