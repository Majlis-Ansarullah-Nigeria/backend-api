using HRS.Api.Common;
using HRS.Application.Features.Notifications.Center;
using HRS.Application.Features.Notifications.NotificationTemplates;
using HRS.Application.Features.Notifications.Push;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class NotificationsEndpoints
{
    public static void MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        var templates = app.MapGroup("/api/v1/secretariat/notification-templates").WithTags("Notifications").RequireAuthorization();

        templates.MapGet("/", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetNotificationTemplatesQuery(user.GetUserId()), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetNotificationTemplates")
            .Produces<IReadOnlyList<NotificationTemplateSummary>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        templates.MapPut("/{id:guid}", async (
                Guid id,
                [FromBody] UpdateNotificationTemplateRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateNotificationTemplateCommand(user.GetUserId(), id, request.Subject, request.Body);
                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("UpdateNotificationTemplate")
            .Produces<NotificationTemplateSummary>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var myNotifications = app.MapGroup("/api/v1/me/notifications").WithTags("Notifications").RequireAuthorization();

        myNotifications.MapGet("/", async (
                NotificationFilter filter,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetMyNotificationsQuery(user.GetUserId(), filter), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetMyNotifications")
            .Produces<IReadOnlyList<NotificationSummary>>();

        myNotifications.MapGet("/unread-count", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetUnreadNotificationCountQuery(user.GetUserId()), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetUnreadNotificationCount")
            .Produces<int>();

        myNotifications.MapPost("/{id:guid}/read", async (
                Guid id,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new MarkNotificationReadCommand(user.GetUserId(), id), cancellationToken);
                return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
            })
            .WithName("MarkNotificationRead")
            .ProducesProblem(StatusCodes.Status404NotFound);

        myNotifications.MapPost("/read-all", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new MarkAllNotificationsReadCommand(user.GetUserId()), cancellationToken);
                return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
            })
            .WithName("MarkAllNotificationsRead");

        var pushSubscriptions = app.MapGroup("/api/v1/me/push-subscriptions").WithTags("Notifications").RequireAuthorization();

        pushSubscriptions.MapGet("/vapid-public-key", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetVapidPublicKeyQuery(), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetVapidPublicKey")
            .Produces<string>();

        pushSubscriptions.MapPost("/", async (
                [FromBody] SavePushSubscriptionRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new SavePushSubscriptionCommand(user.GetUserId(), request.Endpoint, request.P256Dh, request.Auth);
                var result = await sender.Send(command, cancellationToken);
                return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
            })
            .WithName("SavePushSubscription");

        pushSubscriptions.MapDelete("/", async (
                [FromQuery(Name = "endpoint")] string endpoint,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new DeletePushSubscriptionCommand(user.GetUserId(), endpoint), cancellationToken);
                return result.IsSuccess ? Results.Ok() : result.ToProblemDetails();
            })
            .WithName("DeletePushSubscription");
    }
}

public sealed record UpdateNotificationTemplateRequest(string Subject, string Body);

public sealed record SavePushSubscriptionRequest(string Endpoint, string P256Dh, string Auth);
