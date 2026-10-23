using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Mfa;

public sealed class ConfirmTotpEnrollmentCommandHandler(IIdentityService identityService, IAuditLog auditLog)
    : IRequestHandler<ConfirmTotpEnrollmentCommand, Result<IReadOnlyList<string>>>
{
    public async ValueTask<Result<IReadOnlyList<string>>> Handle(
        ConfirmTotpEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var result = await identityService.ConfirmTotpEnrollmentAsync(request.UserId, request.Code, cancellationToken);
        if (result.IsSuccess)
        {
            await auditLog.RecordAsync(
                new AuditEntry(AuditActions.MfaEnabled, request.UserId, "user", request.UserId.ToString(), "Turned on two-step verification"),
                cancellationToken);
        }

        return result;
    }
}
