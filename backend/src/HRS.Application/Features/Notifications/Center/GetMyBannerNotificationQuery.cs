using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications.Center;

/// <summary>FR-NOT-006's "Banner on home" — the single latest unread banner-flagged
/// notification, or none. Dismissing the banner is just marking that notification read (the
/// existing M8.2 endpoint), so there's no separate dismiss concept to model.</summary>
public sealed record GetMyBannerNotificationQuery(Guid UserId) : IRequest<Result<NotificationSummary?>>;

public sealed class GetMyBannerNotificationQueryHandler(IAppDbContext db) : IRequestHandler<GetMyBannerNotificationQuery, Result<NotificationSummary?>>
{
    public async ValueTask<Result<NotificationSummary?>> Handle(GetMyBannerNotificationQuery request, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == request.UserId && n.IsBanner && n.ReadAt == null)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationSummary(n.Id, n.EventType, n.Subject, n.Body, n.Link, n.CreatedAt, n.ReadAt))
            .FirstOrDefaultAsync(cancellationToken);

        return Result.Success(notification);
    }
}
