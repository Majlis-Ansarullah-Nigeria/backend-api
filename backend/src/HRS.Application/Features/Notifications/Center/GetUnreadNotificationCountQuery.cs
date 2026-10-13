using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications.Center;

public sealed record GetUnreadNotificationCountQuery(Guid UserId) : IRequest<Result<int>>;

public sealed class GetUnreadNotificationCountQueryHandler(IAppDbContext db) : IRequestHandler<GetUnreadNotificationCountQuery, Result<int>>
{
    public async ValueTask<Result<int>> Handle(GetUnreadNotificationCountQuery request, CancellationToken cancellationToken)
    {
        var count = await db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == request.UserId && n.ReadAt == null, cancellationToken);

        return Result.Success(count);
    }
}
