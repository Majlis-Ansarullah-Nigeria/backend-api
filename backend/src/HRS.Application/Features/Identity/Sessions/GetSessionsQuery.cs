using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Identity.Sessions;

public sealed record GetSessionsQuery(Guid UserId, Guid CurrentSessionId) : IRequest<Result<IReadOnlyList<SessionSummary>>>;

public sealed class GetSessionsQueryHandler(IIdentityService identityService)
    : IRequestHandler<GetSessionsQuery, Result<IReadOnlyList<SessionSummary>>>
{
    public async ValueTask<Result<IReadOnlyList<SessionSummary>>> Handle(
        GetSessionsQuery request, CancellationToken cancellationToken) =>
        Result.Success(await identityService.GetActiveSessionsAsync(request.UserId, request.CurrentSessionId, cancellationToken));
}
