using HRS.Api.Common;
using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Models;
using HRS.Application.Features.Identity.Login;
using HRS.Application.Features.Identity.Logout;
using HRS.Application.Features.Identity.CurrentUser;
using HRS.Application.Features.Identity.Mfa;
using HRS.Application.Features.Identity.Otp;
using HRS.Application.Features.Identity.Refresh;
using HRS.Application.Features.Identity.Sessions;
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

                return result.IsFailure ? result.ToProblemDetails() : HandleLoginOutcome(result.Value, httpContext.Response);
            })
            .WithName("Login")
            .Produces<MeResponse>()
            .Produces<MfaChallengeResponse>(StatusCodes.Status202Accepted)
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

                return result.IsFailure ? result.ToProblemDetails() : HandleLoginOutcome(result.Value, httpContext.Response);
            })
            .WithName("VerifyOtp")
            .Produces<MeResponse>()
            .Produces<MfaChallengeResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        auth.MapPost("/mfa/verify", async (
                [FromBody] MfaVerifyRequest request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new VerifyMfaCommand(
                    request.ChallengeToken,
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
            .WithName("VerifyMfa")
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

        me.MapPost("/mfa/totp/setup", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new BeginTotpEnrollmentCommand(user.GetUserId()), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("BeginTotpEnrollment")
            .Produces<TotpEnrollment>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        me.MapPost("/mfa/totp/confirm", async (
                [FromBody] ConfirmTotpEnrollmentRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new ConfirmTotpEnrollmentCommand(user.GetUserId(), request.Code);
                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess
                    ? Results.Ok(new ConfirmTotpEnrollmentResponse(result.Value))
                    : result.ToProblemDetails();
            })
            .WithName("ConfirmTotpEnrollment")
            .Produces<ConfirmTotpEnrollmentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        me.MapPost("/mfa/totp/disable", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new DisableTotpCommand(user.GetUserId()), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("DisableTotp")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        me.MapGet("/sessions", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var query = new GetSessionsQuery(user.GetUserId(), user.GetSessionId());
                var result = await sender.Send(query, cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetSessions")
            .Produces<IReadOnlyList<SessionSummary>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        me.MapPost("/sessions/{sessionId:guid}/revoke", async (
                Guid sessionId,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new RevokeSessionCommand(user.GetUserId(), sessionId, user.GetSessionId());
                var result = await sender.Send(command, cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("RevokeSession")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        me.MapPost("/sessions/revoke-others", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await sender.Send(new RevokeOtherSessionsCommand(user.GetUserId(), user.GetSessionId()), cancellationToken);

                return Results.NoContent();
            })
            .WithName("RevokeOtherSessions")
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static IResult HandleLoginOutcome(LoginOutcome outcome, HttpResponse response) => outcome switch
    {
        LoggedIn loggedIn => SetCookiesAndReturnMe(loggedIn.Result, response),
        MfaChallengeRequired mfa => Results.Accepted(value: new MfaChallengeResponse(mfa.ChallengeToken)),
        _ => throw new InvalidOperationException($"Unhandled login outcome: {outcome.GetType()}."),
    };

    private static IResult SetCookiesAndReturnMe(AuthResult result, HttpResponse response)
    {
        response.SetAuthCookies(result.Tokens);

        return Results.Ok(new MeResponse(result.UserId, result.Name, result.Email));
    }
}

public sealed record LoginRequest(string Identifier, string Password);

public sealed record OtpRequest(string Identifier);

public sealed record OtpVerifyRequest(string Identifier, string Code);

public sealed record MeResponse(Guid Id, string Name, string Email);

public sealed record SetActiveAssignmentRequest(Guid RoleAssignmentId);

public sealed record MfaChallengeResponse(string ChallengeToken);

public sealed record MfaVerifyRequest(string ChallengeToken, string Code);

public sealed record ConfirmTotpEnrollmentRequest(string Code);

public sealed record ConfirmTotpEnrollmentResponse(IReadOnlyList<string> RecoveryCodes);
