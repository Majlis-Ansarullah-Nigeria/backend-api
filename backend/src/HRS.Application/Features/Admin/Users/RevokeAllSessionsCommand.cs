using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Admin.Users;

public sealed record RevokeAllSessionsCommand(Guid ActorUserId, Guid TargetUserId) : IRequest<Result>;

public sealed class RevokeAllSessionsCommandHandler(IAccessPolicy accessPolicy, IIdentityService identityService)
    : IRequestHandler<RevokeAllSessionsCommand, Result>
{
    public async ValueTask<Result> Handle(RevokeAllSessionsCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsOrganizationAdminAsync(request.ActorUserId, cancellationToken))
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "Only the Secretariat or an Admin can manage users."));
        }

        await identityService.RevokeAllSessionsAsync(request.TargetUserId, cancellationToken);

        return Result.Success();
    }
}
