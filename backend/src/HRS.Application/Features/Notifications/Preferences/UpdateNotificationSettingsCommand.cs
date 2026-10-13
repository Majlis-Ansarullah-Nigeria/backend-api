using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using DomainUserNotificationSettings = HRS.Domain.Notifications.UserNotificationSettings;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications.Preferences;

public sealed record UpdateNotificationSettingsCommand(Guid UserId, bool DigestEnabled) : IRequest<Result>;

public sealed class UpdateNotificationSettingsCommandHandler(IAppDbContext db) : IRequestHandler<UpdateNotificationSettingsCommand, Result>
{
    public async ValueTask<Result> Handle(UpdateNotificationSettingsCommand request, CancellationToken cancellationToken)
    {
        var existing = await db.UserNotificationSettings.FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken);

        if (existing is not null)
        {
            existing.SetDigestEnabled(request.DigestEnabled);
        }
        else
        {
            db.UserNotificationSettings.Add(DomainUserNotificationSettings.Create(request.UserId, request.DigestEnabled));
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
