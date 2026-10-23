using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Admin.Users;

public sealed record AdminResetMfaCommand(Guid ActorUserId, Guid TargetUserId) : IRequest<Result>;

public sealed class AdminResetMfaCommandHandler(IAccessPolicy accessPolicy, IIdentityService identityService, IAuditLog auditLog)
    : IRequestHandler<AdminResetMfaCommand, Result>
{
    public async ValueTask<Result> Handle(AdminResetMfaCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsOrganizationAdminAsync(request.ActorUserId, cancellationToken))
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "Only the Secretariat or an Admin can manage users."));
        }

        await identityService.AdminResetMfaAsync(request.TargetUserId, cancellationToken);
        await auditLog.RecordAsync(
            new AuditEntry(AuditActions.MfaReset, request.ActorUserId, "user", request.TargetUserId.ToString(), "Reset two-step verification"),
            cancellationToken);

        return Result.Success();
    }
}
