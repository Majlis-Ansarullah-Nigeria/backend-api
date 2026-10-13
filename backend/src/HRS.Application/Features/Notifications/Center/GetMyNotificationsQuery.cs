using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Domain.Notifications;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications.Center;

public enum NotificationFilter
{
    All,
    Unread,
    Deadlines,
}

/// <summary>FR-NOT-001's notification centre list, matching the mockup's three tabs. "Deadlines"
/// is every event under NotificationCategory.DeadlineReminders (the same grouping FR-NOT-004's
/// preferences use) plus WindowOpened, which has its own preference row but still reads as a
/// deadline-ish event here. Most-recent 50, no pagination — kept deliberately simple for v1 (SRS
/// doesn't ask for more).</summary>
public sealed record GetMyNotificationsQuery(Guid UserId, NotificationFilter Filter) : IRequest<Result<IReadOnlyList<NotificationSummary>>>;

public sealed class GetMyNotificationsQueryHandler(IAppDbContext db) : IRequestHandler<GetMyNotificationsQuery, Result<IReadOnlyList<NotificationSummary>>>
{
    private const int MaxResults = 50;

    private static readonly HashSet<NotificationEventType> DeadlineEventTypes = Enum.GetValues<NotificationEventType>()
        .Where(eventType => eventType == NotificationEventType.WindowOpened
            || NotificationCategoryMap.For(eventType) == NotificationCategory.DeadlineReminders)
        .ToHashSet();

    public async ValueTask<Result<IReadOnlyList<NotificationSummary>>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == request.UserId);

        query = request.Filter switch
        {
            NotificationFilter.Unread => query.Where(n => n.ReadAt == null),
            NotificationFilter.Deadlines => query.Where(n => DeadlineEventTypes.Contains(n.EventType)),
            _ => query,
        };

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(MaxResults)
            .Select(n => new NotificationSummary(n.Id, n.EventType, n.Subject, n.Body, n.Link, n.CreatedAt, n.ReadAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<NotificationSummary>>(notifications);
    }
}
