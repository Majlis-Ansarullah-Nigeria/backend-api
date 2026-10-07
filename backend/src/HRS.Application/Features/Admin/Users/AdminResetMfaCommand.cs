using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Admin.Users;

public sealed record AdminResetMfaCommand(Guid ActorUserId, Guid TargetUserId) : IRequest<Result>;

public sealed class AdminResetMfaCommandHandler(IAccessPolicy accessPolicy, IIdentityService identityService)
    : IRequestHandler<AdminResetMfaCommand, Result>
{
    public async ValueTask<Result> Handle(AdminResetMfaCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsOrganizationAdminAsync(request.ActorUserId, cancellationToken))
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "Only the Secretariat or an Admin can manage users."));
        }

        await identityService.AdminResetMfaAsync(request.TargetUserId, cancellationToken);

        return Result.Success();
    }
}
