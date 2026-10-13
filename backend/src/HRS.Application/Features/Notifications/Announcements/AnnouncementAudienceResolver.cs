using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Notifications;
using HRS.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Notifications.Announcements;

/// <summary>FR-NOT-006's "Send to" audience — every currently-active unit head, optionally
/// narrowed to one level. Distinct user ids: a delegate-elevated-to-head or a person heading two
/// units would otherwise be double-counted/double-notified.</summary>
public static class AnnouncementAudienceResolver
{
    public static async Task<List<Guid>> ResolveRecipientUserIdsAsync(
        IAppDbContext db, AnnouncementAudience audience, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var query = db.RoleAssignments.AsNoTracking()
            .Where(a => a.Role == RoleName.UnitHead && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now));

        if (audience != AnnouncementAudience.AllHeads)
        {
            var levelCode = audience switch
            {
                AnnouncementAudience.ZoneHeads => UnitLevelCode.Zone,
                AnnouncementAudience.DilaHeads => UnitLevelCode.Dila,
                AnnouncementAudience.MuqamHeads => UnitLevelCode.Muqam,
                _ => throw new ArgumentOutOfRangeException(nameof(audience)),
            };

            var levelId = await db.UnitLevels.AsNoTracking()
                .Where(l => l.Code == levelCode)
                .Select(l => l.Id)
                .SingleAsync(cancellationToken);

            var unitIdsAtLevel = db.Units.AsNoTracking()
                .Where(u => u.LevelId == levelId && u.ArchivedAt == null)
                .Select(u => u.Id);

            query = query.Where(a => unitIdsAtLevel.Contains(a.UnitId));
        }

        return await query.Select(a => a.UserId).Distinct().ToListAsync(cancellationToken);
    }
}
