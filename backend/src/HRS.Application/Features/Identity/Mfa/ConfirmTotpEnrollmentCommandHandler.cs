using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Mfa;

public sealed class ConfirmTotpEnrollmentCommandHandler(IIdentityService identityService)
    : IRequestHandler<ConfirmTotpEnrollmentCommand, Result<IReadOnlyList<string>>>
{
    public async ValueTask<Result<IReadOnlyList<string>>> Handle(
        ConfirmTotpEnrollmentCommand request, CancellationToken cancellationToken) =>
        await identityService.ConfirmTotpEnrollmentAsync(request.UserId, request.Code, cancellationToken);
}
