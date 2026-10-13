using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Domain.Notifications;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications.Preferences;

/// <summary>Always returns all 5 categories (the Profile mockup's 5 rows), defaulting an
/// uncustomized one to both channels on — a brand-new user has no NotificationPreference rows at
/// all, not five default ones, so the frontend never has to special-case "no row yet".</summary>
public sealed record GetMyNotificationPreferencesQuery(Guid UserId) : IRequest<Result<IReadOnlyList<NotificationPreferenceView>>>;

public sealed class GetMyNotificationPreferencesQueryHandler(IAppDbContext db)
    : IRequestHandler<GetMyNotificationPreferencesQuery, Result<IReadOnlyList<NotificationPreferenceView>>>
{
    private static readonly NotificationCategory[] AllCategories = Enum.GetValues<NotificationCategory>();

    public async ValueTask<Result<IReadOnlyList<NotificationPreferenceView>>> Handle(
        GetMyNotificationPreferencesQuery request, CancellationToken cancellationToken)
    {
        var existing = await db.NotificationPreferences.AsNoTracking()
            .Where(p => p.UserId == request.UserId)
            .ToDictionaryAsync(p => p.Category, cancellationToken);

        IReadOnlyList<NotificationPreferenceView> views = AllCategories
            .Select(category => existing.TryGetValue(category, out var preference)
                ? new NotificationPreferenceView(category, preference.InAppEnabled, preference.EmailEnabled)
                : new NotificationPreferenceView(category, InAppEnabled: true, EmailEnabled: true))
            .ToList();

        return Result.Success(views);
    }
}
