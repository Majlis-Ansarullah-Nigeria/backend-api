using HRS.Api.Common;
using HRS.Application.Features.Analytics;
using HRS.Domain.Organization;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class DashboardsEndpoints
{
    public static void MapDashboardsEndpoints(this IEndpointRouteBuilder app)
    {
        var dashboards = app.MapGroup("/api/v1/dashboards").WithTags("Dashboards").RequireAuthorization();

        dashboards.MapGet("/compliance", async (
                [FromQuery] Guid? reportTypeId,
                [FromQuery] UnitLevelCode? level,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetComplianceDashboardQuery(user.GetUserId(), reportTypeId, level), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetComplianceDashboard")
            .Produces<ComplianceDashboard>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        dashboards.MapPost("/compliance/reminders", async (
                [FromBody] SendComplianceRemindersRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new SendComplianceRemindersCommand(user.GetUserId(), request.WindowId, request.Level, request.GroupUnitId);
                var result = await sender.Send(command, cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("SendComplianceReminders")
            .Produces<SendRemindersResult>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/api/v1/secretariat/overview", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetSecretariatOverviewQuery(user.GetUserId()), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithTags("Dashboards")
            .RequireAuthorization()
            .WithName("GetSecretariatOverview")
            .Produces<SecretariatOverview>()
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}

/// <summary>GroupUnitId is the table row: a zone's id, or a department's own id.</summary>
public sealed record SendComplianceRemindersRequest(Guid WindowId, UnitLevelCode Level, Guid? GroupUnitId);
