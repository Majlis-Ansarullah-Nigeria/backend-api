using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.CurrentUser;

public sealed record GetMeQuery(Guid UserId) : IRequest<Result<AuthenticatedUser>>;

public sealed class GetMeQueryHandler(IIdentityService identityService) : IRequestHandler<GetMeQuery, Result<AuthenticatedUser>>
{
    public async ValueTask<Result<AuthenticatedUser>> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var user = await identityService.GetUserAsync(request.UserId, cancellationToken);

        return user is null
            ? Result.Failure<AuthenticatedUser>(Error.NotFound("USER_NOT_FOUND", "User not found."))
            : Result.Success(user);
    }
}
