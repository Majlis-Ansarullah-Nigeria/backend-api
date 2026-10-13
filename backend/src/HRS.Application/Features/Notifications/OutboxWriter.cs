using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications;

/// <summary>Two small reusable primitives for FR-NOT-002: every event type just needs "queue this
/// for the unit's current active head(s)" or "queue this for one specific user" — both write a
/// durable OutboxMessage row in the caller's own transaction (the caller still calls
/// SaveChangesAsync), never send anything directly. Replaces M7.2's ReportNotifier, which sent
/// email synchronously because no outbox existed yet.</summary>
public static class OutboxWriter
{
    public static async Task EnqueueForUnitHeadsAsync(
        IAppDbContext db, Guid unitId, NotificationEventType eventType, IReadOnlyDictionary<string, string> placeholders,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var headUserIds = await db.RoleAssignments
            .AsNoTracking()
            .Where(a => a.Role == RoleName.UnitHead && a.UnitId == unitId
                && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .Select(a => a.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var headUserId in headUserIds)
        {
            EnqueueForUser(db, headUserId, eventType, placeholders, now);
        }
    }

    public static void EnqueueForUser(
        IAppDbContext db, Guid recipientUserId, NotificationEventType eventType, IReadOnlyDictionary<string, string> placeholders,
        DateTimeOffset now)
    {
        var payloadJson = JsonSerializer.Serialize(placeholders);
        db.OutboxMessages.Add(OutboxMessage.Create(Guid.CreateVersion7(), eventType, recipientUserId, payloadJson, now));
    }
}
