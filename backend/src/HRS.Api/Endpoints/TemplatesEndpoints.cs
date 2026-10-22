using HRS.Api.Common;
using HRS.Application.Features.Templates.FieldTargets;
using HRS.Application.Features.Templates.ReportTypes;
using HRS.Application.Features.Templates.TemplateVersions;
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
                    request.AllowAttachments,
                    request.AllowNilReport);

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
                    request.AllowAttachments,
                    request.AllowNilReport);

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

        reportTypes.MapGet("/{id:guid}/field-targets", async (
                Guid id,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetFieldTargetsQuery(user.GetUserId(), id), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetFieldTargets")
            .Produces<IReadOnlyList<FieldTargetDto>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        reportTypes.MapPut("/{id:guid}/field-targets/{fieldKey}", async (
                Guid id,
                string fieldKey,
                [FromBody] SetFieldTargetsRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new SetFieldTargetsCommand(user.GetUserId(), id, fieldKey, request.Targets);
                var result = await sender.Send(command, cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("SetFieldTargets")
            .Produces<IReadOnlyList<FieldTargetDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var templateVersions = app.MapGroup("/api/v1/report-types/{reportTypeId:guid}/template-versions")
            .WithTags("Templates")
            .RequireAuthorization();

        templateVersions.MapGet("/", async (
                Guid reportTypeId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetTemplateVersionsQuery(reportTypeId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetTemplateVersions")
            .Produces<IReadOnlyList<TemplateVersionSummary>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        templateVersions.MapGet("/{versionId:guid}", async (
                Guid reportTypeId,
                Guid versionId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetTemplateVersionQuery(reportTypeId, versionId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetTemplateVersion")
            .Produces<TemplateVersionDetail>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        templateVersions.MapPost("/", async (
                Guid reportTypeId,
                [FromBody] UpdateTemplateSchemaRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateTemplateSchemaCommand(user.GetUserId(), reportTypeId, request.Schema);
                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("UpdateTemplateSchema")
            .Produces<TemplateVersionSummary>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        templateVersions.MapPost("/{versionId:guid}/publish", async (
                Guid reportTypeId,
                Guid versionId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new PublishTemplateVersionCommand(user.GetUserId(), reportTypeId, versionId);
                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("PublishTemplateVersion")
            .Produces<TemplateVersionSummary>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
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
    bool AllowAttachments,
    bool AllowNilReport);

public sealed record UpdateReportTypeRequest(
    string Name,
    string? Description,
    ReportTypeCategory Category,
    ReportTypeFrequency Frequency,
    IReadOnlyList<UnitLevelCode> ApplicableLevels,
    UnitLevelCode RecipientLevel,
    int ObserverDepth,
    bool AllowAttachments,
    bool AllowNilReport);

public sealed record UpdateTemplateSchemaRequest(TemplateSchema Schema);

public sealed record SetFieldTargetsRequest(IReadOnlyList<FieldTargetLevelValue> Targets);
