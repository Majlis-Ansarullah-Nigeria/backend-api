using HRS.Api.Common;
using HRS.Application.Features.Identity.Login;
using HRS.Application.Features.Identity.Logout;
using HRS.Application.Features.Identity.CurrentUser;
using HRS.Application.Features.Identity.Otp;
using HRS.Application.Features.Identity.Refresh;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class IdentityEndpoints
{
    public static void MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithTags("Identity").RequireRateLimiting("auth");

        auth.MapPost("/login", async (
                [FromBody] LoginRequest request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new LoginCommand(
                    request.Identifier,
                    request.Password,
                    httpContext.Connection.RemoteIpAddress?.ToString(),
                    httpContext.Request.Headers.UserAgent.ToString());

                var result = await sender.Send(command, cancellationToken);

                if (result.IsFailure)
                {
                    return result.ToProblemDetails();
                }

                httpContext.Response.SetAuthCookies(result.Value.Tokens);

                return Results.Ok(new MeResponse(result.Value.UserId, result.Value.Name, result.Value.Email));
            })
            .WithName("Login")
            .Produces<MeResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        auth.MapPost("/refresh", async (
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var refreshToken = httpContext.Request.Cookies[AuthCookies.RefreshToken];

                if (string.IsNullOrEmpty(refreshToken))
                {
                    return Results.Unauthorized();
                }

                var command = new RefreshCommand(
                    refreshToken,
                    httpContext.Connection.RemoteIpAddress?.ToString(),
                    httpContext.Request.Headers.UserAgent.ToString());

                var result = await sender.Send(command, cancellationToken);

                if (result.IsFailure)
                {
                    httpContext.Response.ClearAuthCookies();
                    return result.ToProblemDetails();
                }

                httpContext.Response.SetAuthCookies(result.Value);

                return Results.NoContent();
            })
            .WithName("RefreshSession")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/otp/request", async (
                [FromBody] OtpRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new RequestOtpCommand(request.Identifier), cancellationToken);

                return result.IsFailure
                    ? result.ToProblemDetails()
                    : Results.Accepted();
            })
            .WithName("RequestOtp")
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/otp/verify", async (
                [FromBody] OtpVerifyRequest request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new VerifyOtpCommand(
                    request.Identifier,
                    request.Code,
                    httpContext.Connection.RemoteIpAddress?.ToString(),
                    httpContext.Request.Headers.UserAgent.ToString());

                var result = await sender.Send(command, cancellationToken);

                if (result.IsFailure)
                {
                    return result.ToProblemDetails();
                }

                httpContext.Response.SetAuthCookies(result.Value.Tokens);

                return Results.Ok(new MeResponse(result.Value.UserId, result.Value.Name, result.Value.Email));
            })
            .WithName("VerifyOtp")
            .Produces<MeResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        auth.MapPost("/logout", async (
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var refreshToken = httpContext.Request.Cookies[AuthCookies.RefreshToken];

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    await sender.Send(new LogoutCommand(refreshToken), cancellationToken);
                }

                httpContext.Response.ClearAuthCookies();

                return Results.NoContent();
            })
            .WithName("Logout");

        var me = app.MapGroup("/api/v1/me").WithTags("Identity").RequireAuthorization();

        me.MapGet("/", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetMeQuery(user.GetUserId()), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetMe")
            .Produces<MeProfile>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        me.MapPut("/active-assignment", async (
                [FromBody] SetActiveAssignmentRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new SetActiveAssignmentCommand(user.GetUserId(), request.RoleAssignmentId);
                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("SetActiveAssignment")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

public sealed record LoginRequest(string Identifier, string Password);

public sealed record OtpRequest(string Identifier);

public sealed record OtpVerifyRequest(string Identifier, string Code);

public sealed record MeResponse(Guid Id, string Name, string Email);

public sealed record SetActiveAssignmentRequest(Guid RoleAssignmentId);
