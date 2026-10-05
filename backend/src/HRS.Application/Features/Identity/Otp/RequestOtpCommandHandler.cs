using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Otp;

public sealed class RequestOtpCommandHandler(IIdentityService identityService)
    : IRequestHandler<RequestOtpCommand, Result>
{
    public ValueTask<Result> Handle(RequestOtpCommand request, CancellationToken cancellationToken) =>
        new(identityService.RequestOtpAsync(request.Identifier, cancellationToken));
}
