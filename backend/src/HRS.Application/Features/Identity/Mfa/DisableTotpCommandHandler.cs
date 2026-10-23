using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Domain.Access;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Mfa;

public sealed class DisableTotpCommandHandler(IIdentityService identityService, IAccessPolicy accessPolicy, IAuditLog auditLog)
    : IRequestHandler<DisableTotpCommand, Result>
{
    public async ValueTask<Result> Handle(DisableTotpCommand request, CancellationToken cancellationToken)
    {
        // FR-IAM-003: mandatory for Secretariat/Admin — can't be turned back off while either
        // role is currently active, even though the server can't force it *on* for someone who
        // never enrolled (that would be an unrecoverable lockout).
        var isMandatory = await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken)
            || await accessPolicy.HasRoleAsync(request.UserId, RoleName.SysAdmin, cancellationToken);

        if (isMandatory)
        {
            return Result.Failure(Error.Forbidden(
                "MFA_REQUIRED_FOR_ROLE",
                "Two-step sign-in is mandatory for Secretariat and Admin accounts and can't be turned off."));
        }

        await identityService.DisableTotpAsync(request.UserId, cancellationToken);
        await auditLog.RecordAsync(
            new AuditEntry(AuditActions.MfaDisabled, request.UserId, "user", request.UserId.ToString(), "Turned off two-step verification"),
            cancellationToken);

        return Result.Success();
    }
}
