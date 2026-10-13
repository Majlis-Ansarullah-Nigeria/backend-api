using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications.Center;

/// <summary>Idempotent — re-marking an already-read notification is a no-op, never an error (the
/// user may tap it again, or two tabs may race). A notification belonging to someone else 404s
/// rather than 403s, so the id's existence isn't leaked (IDOR prevention, same reasoning as every
/// other resource-scoped handler in this codebase).</summary>
public sealed record MarkNotificationReadCommand(Guid UserId, Guid NotificationId) : IRequest<Result>;

public sealed class MarkNotificationReadCommandHandler(IAppDbContext db, IClock clock) : IRequestHandler<MarkNotificationReadCommand, Result>
{
    public async ValueTask<Result> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId && n.UserId == request.UserId, cancellationToken);

        if (notification is null)
        {
            return Result.Failure(Error.NotFound("NOTIFICATION_NOT_FOUND", "That notification doesn't exist."));
        }

        notification.MarkRead(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
