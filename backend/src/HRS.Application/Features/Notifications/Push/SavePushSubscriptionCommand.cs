using HRS.Application.Common.Interfaces;
using HRS.Domain.Admin;
using HRS.Domain.Common;
using HRS.Domain.Notifications;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications.Push;

/// <summary>Upserts by Endpoint — a browser may call subscribe() again with the same endpoint
/// (e.g. after reinstalling the PWA) and that should never create a duplicate row.</summary>
public sealed record SavePushSubscriptionCommand(Guid UserId, string Endpoint, string P256Dh, string Auth) : IRequest<Result>, IRequiresFeature
{
    public FeatureModule Feature => FeatureModule.WebPush;
}

public sealed class SavePushSubscriptionCommandHandler(IAppDbContext db, IClock clock) : IRequestHandler<SavePushSubscriptionCommand, Result>
{
    public async ValueTask<Result> Handle(SavePushSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var existing = await db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint, cancellationToken);

        if (existing is not null)
        {
            existing.UpdateKeys(request.P256Dh, request.Auth);
        }
        else
        {
            db.PushSubscriptions.Add(PushSubscription.Create(
                Guid.CreateVersion7(), request.UserId, request.Endpoint, request.P256Dh, request.Auth, clock.UtcNow));
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
