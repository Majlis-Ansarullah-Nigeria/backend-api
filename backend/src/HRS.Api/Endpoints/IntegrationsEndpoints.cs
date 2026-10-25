using HRS.Api.Common;
using HRS.Application.Features.Admin.Features;
using HRS.Application.Features.Admin.Webhooks;
using HRS.Domain.Admin;
using HRS.Domain.Webhooks;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class IntegrationsEndpoints
{
    public static void MapIntegrationsEndpoints(this IEndpointRouteBuilder app)
    {
        var flags = app.MapGroup("/api/v1/feature-flags").WithTags("Settings").RequireAuthorization();

        flags.MapGet("/", async (System.Security.Claims.ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetFeatureFlagsQuery(user.GetUserId()), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetFeatureFlags")
            .Produces<FeatureFlagsView>();

        flags.MapPut("/{module}", async (
                FeatureModule module,
                [FromBody] SetFeatureFlagRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new SetFeatureFlagCommand(user.GetUserId(), module, request.Enabled), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("SetFeatureFlag")
            .Produces<FeatureFlagDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var webhooks = app.MapGroup("/api/v1/webhooks").WithTags("Settings").RequireAuthorization();

        webhooks.MapGet("/", async (System.Security.Claims.ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetWebhooksQuery(user.GetUserId()), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetWebhooks")
            .Produces<IReadOnlyList<WebhookEndpointDto>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        webhooks.MapPost("/", async (
                [FromBody] SaveWebhookRequest request, System.Security.Claims.ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new SaveWebhookCommand(user.GetUserId(), null, request.Url, request.Events, request.IsActive), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("CreateWebhook")
            .Produces<WebhookSecretResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        webhooks.MapPut("/{id:guid}", async (
                Guid id, [FromBody] SaveWebhookRequest request, System.Security.Claims.ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new SaveWebhookCommand(user.GetUserId(), id, request.Url, request.Events, request.IsActive), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value.Endpoint) : result.ToProblemDetails();
            })
            .WithName("UpdateWebhook")
            .Produces<WebhookEndpointDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        webhooks.MapDelete("/{id:guid}", async (Guid id, System.Security.Claims.ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new DeleteWebhookCommand(user.GetUserId(), id), cancellationToken);
                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("DeleteWebhook")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        webhooks.MapPost("/{id:guid}/rotate-secret", async (Guid id, System.Security.Claims.ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new RotateWebhookSecretCommand(user.GetUserId(), id), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("RotateWebhookSecret")
            .Produces<WebhookSecretResult>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        webhooks.MapPost("/{id:guid}/test", async (Guid id, System.Security.Claims.ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new SendTestWebhookCommand(user.GetUserId(), id), cancellationToken);
                return result.IsSuccess ? Results.Accepted() : result.ToProblemDetails();
            })
            .WithName("SendTestWebhook")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        webhooks.MapGet("/{id:guid}/deliveries", async (Guid id, System.Security.Claims.ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetWebhookDeliveriesQuery(user.GetUserId(), id), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetWebhookDeliveries")
            .Produces<IReadOnlyList<WebhookDeliveryDto>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}

public sealed record SetFeatureFlagRequest(bool Enabled);

public sealed record SaveWebhookRequest(string Url, IReadOnlyList<WebhookEvent> Events, bool IsActive = true);
