using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Admin.Users;

public sealed record ReactivateUserCommand(Guid ActorUserId, Guid TargetUserId) : IRequest<Result>;

public sealed class ReactivateUserCommandHandler(IAccessPolicy accessPolicy, IIdentityService identityService, IAuditLog auditLog)
    : IRequestHandler<ReactivateUserCommand, Result>
{
    public async ValueTask<Result> Handle(ReactivateUserCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsOrganizationAdminAsync(request.ActorUserId, cancellationToken))
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "Only the Secretariat or an Admin can manage users."));
        }

        var result = await identityService.ReactivateUserAsync(request.TargetUserId, cancellationToken);
        if (result.IsSuccess)
        {
            await auditLog.RecordAsync(
                new AuditEntry(AuditActions.UserReactivated, request.ActorUserId, "user", request.TargetUserId.ToString(), "Reactivated the account"),
                cancellationToken);
        }

        return result;
    }
}
