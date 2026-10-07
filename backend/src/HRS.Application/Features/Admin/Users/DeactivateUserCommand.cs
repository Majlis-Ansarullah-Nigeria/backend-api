using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Admin.Users;

public sealed record DeactivateUserCommand(Guid ActorUserId, Guid TargetUserId) : IRequest<Result>;

public sealed class DeactivateUserCommandHandler(IAccessPolicy accessPolicy, IIdentityService identityService)
    : IRequestHandler<DeactivateUserCommand, Result>
{
    public async ValueTask<Result> Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsOrganizationAdminAsync(request.ActorUserId, cancellationToken))
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "Only the Secretariat or an Admin can manage users."));
        }

        if (request.ActorUserId == request.TargetUserId)
        {
            return Result.Failure(Error.Validation("CANNOT_DEACTIVATE_SELF", "You can't deactivate your own account."));
        }

        return await identityService.DeactivateUserAsync(request.TargetUserId, cancellationToken);
    }
}
