using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Admin.Users;

public sealed record RevokeAllSessionsCommand(Guid ActorUserId, Guid TargetUserId) : IRequest<Result>;

public sealed class RevokeAllSessionsCommandHandler(IAccessPolicy accessPolicy, IIdentityService identityService, IAuditLog auditLog)
    : IRequestHandler<RevokeAllSessionsCommand, Result>
{
    public async ValueTask<Result> Handle(RevokeAllSessionsCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsOrganizationAdminAsync(request.ActorUserId, cancellationToken))
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "Only the Secretariat or an Admin can manage users."));
        }

        await identityService.RevokeAllSessionsAsync(request.TargetUserId, cancellationToken);
        await auditLog.RecordAsync(
            new AuditEntry(AuditActions.SessionsRevoked, request.ActorUserId, "user", request.TargetUserId.ToString(), "Signed out of every session"),
            cancellationToken);

        return Result.Success();
    }
}
