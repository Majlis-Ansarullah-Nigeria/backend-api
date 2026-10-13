using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications.Push;

/// <summary>Idempotent unsubscribe — deleting an endpoint that's already gone (or never belonged
/// to this user) is a no-op, never an error.</summary>
public sealed record DeletePushSubscriptionCommand(Guid UserId, string Endpoint) : IRequest<Result>;

public sealed class DeletePushSubscriptionCommandHandler(IAppDbContext db) : IRequestHandler<DeletePushSubscriptionCommand, Result>
{
    public async ValueTask<Result> Handle(DeletePushSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var existing = await db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint && s.UserId == request.UserId, cancellationToken);

        if (existing is not null)
        {
            db.PushSubscriptions.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
