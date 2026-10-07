using HRS.Api.Common;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Identity.Invitations;
using HRS.Domain.Common;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace HRS.Api.Endpoints;

public static class InvitationsEndpoints
{
    public static void MapInvitationsEndpoints(this IEndpointRouteBuilder app)
    {
        var invitations = app.MapGroup("/api/v1/invitations").WithTags("Invitations");

        invitations.MapPost("/", async (
                [FromBody] CreateInvitationRequest request,
                System.Security.Claims.ClaimsPrincipal user,
                IAccessPolicy accessPolicy,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                // Re-gated per FR-AC-004/FR-ORG-004 (M2.4) — every invite is role-driven in
                // practice (assigning a head/delegate sends one via CreateRoleAssignmentCommand),
                // so a bare invite with no role attached is still an org-admin-only action.
                if (!await accessPolicy.IsOrganizationAdminAsync(user.GetUserId(), cancellationToken))
                {
                    return Result.Failure<CreateInvitationResult>(
                        Error.Forbidden("FORBIDDEN", "Only the Secretariat or an Admin can manage the organization.")).ToProblemDetails();
                }

                var result = await sender.Send(new CreateInvitationCommand(request.Email, request.Name), cancellationToken);

                return result.IsFailure
                    ? result.ToProblemDetails()
                    : Results.Created($"/api/v1/invitations/{result.Value.Id}", result.Value);
            })
            .WithName("CreateInvitation")
            .RequireAuthorization()
            .Produces<CreateInvitationResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        invitations.MapGet("/{token}", async (
                string token,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetInvitationQuery(token), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetInvitation")
            .Produces<InvitationPreview>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var auth = app.MapGroup("/api/v1/auth").WithTags("Identity").RequireRateLimiting("auth");

        auth.MapPost("/invitations/{token}/accept", async (
                string token,
                [FromBody] AcceptInvitationRequest request,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new AcceptInvitationCommand(
                    token,
                    request.Name,
                    request.Phone,
                    request.Password,
                    request.AgreedToPrivacyNotice,
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
            .WithName("AcceptInvitation")
            .Produces<MeResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

public sealed record CreateInvitationRequest(string Email, string Name);

public sealed record AcceptInvitationRequest(string Name, string? Phone, string Password, bool AgreedToPrivacyNotice);
