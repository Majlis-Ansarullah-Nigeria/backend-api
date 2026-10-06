using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Sessions;

public sealed record RevokeSessionCommand(Guid UserId, Guid SessionId, Guid CurrentSessionId) : IRequest<Result>;

public sealed class RevokeSessionCommandHandler(IIdentityService identityService)
    : IRequestHandler<RevokeSessionCommand, Result>
{
    public async ValueTask<Result> Handle(RevokeSessionCommand request, CancellationToken cancellationToken) =>
        await identityService.RevokeSessionByIdAsync(
            request.UserId, request.SessionId, request.CurrentSessionId, cancellationToken);
}
