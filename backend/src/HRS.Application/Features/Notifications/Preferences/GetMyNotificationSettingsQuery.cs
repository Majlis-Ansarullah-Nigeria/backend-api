using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications.Preferences;

public sealed record NotificationSettingsView(bool DigestEnabled);

public sealed record GetMyNotificationSettingsQuery(Guid UserId) : IRequest<Result<NotificationSettingsView>>;

public sealed class GetMyNotificationSettingsQueryHandler(IAppDbContext db) : IRequestHandler<GetMyNotificationSettingsQuery, Result<NotificationSettingsView>>
{
    public async ValueTask<Result<NotificationSettingsView>> Handle(GetMyNotificationSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await db.UserNotificationSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken);

        return Result.Success(new NotificationSettingsView(settings?.DigestEnabled ?? false));
    }
}
