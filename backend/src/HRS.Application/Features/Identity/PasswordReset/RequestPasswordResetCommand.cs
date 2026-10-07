using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.PasswordReset;

public sealed record RequestPasswordResetCommand(string Identifier) : IRequest<Result>;

public sealed class RequestPasswordResetCommandHandler(IIdentityService identityService)
    : IRequestHandler<RequestPasswordResetCommand, Result>
{
    public ValueTask<Result> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken) =>
        new(identityService.RequestPasswordResetAsync(request.Identifier, cancellationToken));
}
