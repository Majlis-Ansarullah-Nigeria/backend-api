using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Sessions;

public sealed record RevokeOtherSessionsCommand(Guid UserId, Guid CurrentSessionId) : IRequest<Result>;

public sealed class RevokeOtherSessionsCommandHandler(IIdentityService identityService)
    : IRequestHandler<RevokeOtherSessionsCommand, Result>
{
    public async ValueTask<Result> Handle(RevokeOtherSessionsCommand request, CancellationToken cancellationToken)
    {
        await identityService.RevokeOtherSessionsAsync(request.UserId, request.CurrentSessionId, cancellationToken);
        return Result.Success();
    }
}
