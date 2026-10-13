using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications.Center;

public sealed record MarkAllNotificationsReadCommand(Guid UserId) : IRequest<Result>;

public sealed class MarkAllNotificationsReadCommandHandler(IAppDbContext db, IClock clock) : IRequestHandler<MarkAllNotificationsReadCommand, Result>
{
    public async ValueTask<Result> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var unread = await db.Notifications
            .Where(n => n.UserId == request.UserId && n.ReadAt == null)
            .ToListAsync(cancellationToken);

        var now = clock.UtcNow;
        foreach (var notification in unread)
        {
            notification.MarkRead(now);
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
