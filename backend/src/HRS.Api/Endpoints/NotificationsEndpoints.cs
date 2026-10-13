using HRS.Api.Common;
using HRS.Application.Features.Notifications.NotificationTemplates;
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
    }
}

public sealed record UpdateNotificationTemplateRequest(string Subject, string Body);
