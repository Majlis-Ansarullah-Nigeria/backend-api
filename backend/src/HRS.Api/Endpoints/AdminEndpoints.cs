using HRS.Api.Common;
using HRS.Application.Features.Admin.Users;
using Mediator;

namespace HRS.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/v1/admin/users").WithTags("Admin").RequireAuthorization();

        users.MapGet("/", async (
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetUsersQuery(user.GetUserId()), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.ToProblemDetails();
            })
            .WithName("GetUsers")
            .Produces<IReadOnlyList<UserListItem>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        users.MapPost("/{id:guid}/deactivate", async (
                Guid id,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new DeactivateUserCommand(user.GetUserId(), id), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("DeactivateUser")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        users.MapPost("/{id:guid}/reactivate", async (
                Guid id,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ReactivateUserCommand(user.GetUserId(), id), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("ReactivateUser")
            .ProducesProblem(StatusCodes.Status403Forbidden);

        users.MapPost("/{id:guid}/reset-mfa", async (
                Guid id,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new AdminResetMfaCommand(user.GetUserId(), id), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("AdminResetMfa")
            .ProducesProblem(StatusCodes.Status403Forbidden);

        users.MapPost("/{id:guid}/revoke-sessions", async (
                Guid id,
                System.Security.Claims.ClaimsPrincipal user,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new RevokeAllSessionsCommand(user.GetUserId(), id), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
            })
            .WithName("RevokeAllSessions")
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
