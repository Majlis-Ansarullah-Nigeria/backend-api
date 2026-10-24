using HRS.Api.Common;
using HRS.Application.Features.Admin.Settings;
using HRS.Domain.Admin;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var settings = app.MapGroup("/api/v1/settings").WithTags("Settings").RequireAuthorization();

        settings.MapGet("/", async (System.Security.Claims.ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetOrganizationSettingsQuery(user.GetUserId()), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetOrganizationSettings")
            .Produces<OrganizationSettingsDto>();

        settings.MapPut("/", async (
                [FromBody] UpdateOrganizationSettingsRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new UpdateOrganizationSettingsCommand(
                    user.GetUserId(), request.Name, request.BrandColor, request.TimeZone, request.DateFormat, request.FiscalYearStartMonth);
                var result = await sender.Send(command, cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("UpdateOrganizationSettings")
            .Produces<OrganizationSettingsDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        settings.MapGet("/time-zones", async (ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetTimeZoneOptionsQuery(), cancellationToken);
                return Results.Ok(result.Value);
            })
            .WithName("GetTimeZoneOptions")
            .Produces<IReadOnlyList<TimeZoneOption>>();

        settings.MapPut("/logo", async (
                IFormFile file,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                if (file.Length > UploadLogoCommandHandler.MaxBytes)
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "The logo must be at most 512 KB.");
                }

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, cancellationToken);
                var result = await sender.Send(new UploadLogoCommand(user.GetUserId(), stream.ToArray()), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("UploadLogo")
            .DisableAntiforgery()
            .Produces<OrganizationSettingsDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        settings.MapDelete("/logo", async (System.Security.Claims.ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new RemoveLogoCommand(user.GetUserId()), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("RemoveLogo")
            .Produces<OrganizationSettingsDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        settings.MapGet("/logo", async (HttpContext httpContext, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetLogoQuery(), cancellationToken);
                if (result.IsFailure)
                {
                    return result.ToProblemDetails();
                }

                // An SVG opened directly could run script; sandbox it and forbid content sniffing.
                httpContext.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
                httpContext.Response.Headers.XContentTypeOptions = "nosniff";
                httpContext.Response.Headers.CacheControl = "private, max-age=86400";
                return Results.File(result.Value.Content, result.Value.ContentType);
            })
            .WithName("GetLogo")
            .Produces(StatusCodes.Status200OK, contentType: "image/png")
            .ProducesProblem(StatusCodes.Status404NotFound);

        var system = app.MapGroup("/api/v1/system").WithTags("Settings").RequireAuthorization();

        system.MapGet("/health-summary", async (System.Security.Claims.ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetSystemHealthQuery(user.GetUserId()), cancellationToken);
                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetSystemHealth")
            .Produces<SystemHealth>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        system.MapPost("/notifications/retry", async (System.Security.Claims.ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new RetryFailedNotificationsCommand(user.GetUserId()), cancellationToken);
                return result.IsSuccess ? Results.Ok(new RetryNotificationsResponse(result.Value)) : result.ToProblemDetails();
            })
            .WithName("RetryFailedNotifications")
            .Produces<RetryNotificationsResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}

public sealed record UpdateOrganizationSettingsRequest(string Name, BrandColor BrandColor, string TimeZone, DateFormatStyle DateFormat, int FiscalYearStartMonth);

public sealed record RetryNotificationsResponse(int Retried);
