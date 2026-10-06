using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Mfa;

public sealed record BeginTotpEnrollmentCommand(Guid UserId) : IRequest<Result<TotpEnrollment>>;

public sealed class BeginTotpEnrollmentCommandHandler(IIdentityService identityService)
    : IRequestHandler<BeginTotpEnrollmentCommand, Result<TotpEnrollment>>
{
    public async ValueTask<Result<TotpEnrollment>> Handle(BeginTotpEnrollmentCommand request, CancellationToken cancellationToken) =>
        await identityService.BeginTotpEnrollmentAsync(request.UserId, cancellationToken);
}
