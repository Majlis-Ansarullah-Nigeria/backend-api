using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Domain.Notifications;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications.Preferences;

/// <summary>One switch at a time, matching the mockup's per-row toggle interaction — not a bulk
/// form save.</summary>
public sealed record UpdateNotificationPreferenceCommand(
    Guid UserId, NotificationCategory Category, bool InAppEnabled, bool EmailEnabled) : IRequest<Result>;

public sealed class UpdateNotificationPreferenceCommandHandler(IAppDbContext db)
    : IRequestHandler<UpdateNotificationPreferenceCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateNotificationPreferenceCommand request, CancellationToken cancellationToken)
    {
        var existing = await db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == request.UserId && p.Category == request.Category, cancellationToken);

        if (existing is not null)
        {
            existing.Update(request.InAppEnabled, request.EmailEnabled);
        }
        else
        {
            db.NotificationPreferences.Add(NotificationPreference.Create(
                Guid.CreateVersion7(), request.UserId, request.Category, request.InAppEnabled, request.EmailEnabled));
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
