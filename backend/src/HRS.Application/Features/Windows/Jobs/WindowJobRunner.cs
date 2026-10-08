using HRS.Application.Common.Interfaces;
using HRS.Domain.Organization;
using HRS.Domain.Windows;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.Jobs;

/// <summary>
/// Detects window-lifecycle transitions (BR-WIN-08: open, remind, overdue, close, escalate) and
/// logs each one exactly once per (window, unit). Pure detection — it never sends a notification
/// itself and never touches a ReportInstance (M5.1 doesn't exist yet, so there is nothing to
/// actually mark Missed); M8's outbox dispatcher is the intended consumer of the rows this writes,
/// and M5.1's instance lifecycle is the intended consumer of a unit's Closed event once it exists.
/// Which stages actually fire is read per-window from WindowReminderSettings (the "Create window"
/// mockup's Reminders panel), not a fixed cadence.
/// </summary>
public static class WindowJobRunner
{
    public static async Task<int> RunAsync(IAppDbContext db, IClock clock, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var windows = await db.SubmissionWindows.AsNoTracking().ToListAsync(cancellationToken);
        if (windows.Count == 0)
        {
            return 0;
        }

        var windowIds = windows.Select(w => w.Id).ToList();
        var existingKeys = await db.WindowJobRuns.AsNoTracking()
            .Where(r => windowIds.Contains(r.WindowId))
            .Select(r => new { r.WindowId, r.UnitId, r.EventType, r.EventKey })
            .ToListAsync(cancellationToken);
        var seen = existingKeys.Select(r => (r.WindowId, r.UnitId, r.EventType, r.EventKey)).ToHashSet();

        var levelIdByCode = await db.UnitLevels.AsNoTracking()
            .ToDictionaryAsync(l => l.Code, l => l.Id, cancellationToken);
        var levelCodeById = levelIdByCode.ToDictionary(kv => kv.Value, kv => kv.Key);

        var created = 0;

        foreach (var window in windows)
        {
            var levelsInWindow = window.DueDates.Select(d => d.Level).ToHashSet();
            if (levelsInWindow.Count == 0)
            {
                continue;
            }

            var unitsQuery = window.TargetUnitIds.Count > 0
                ? db.Units.AsNoTracking().Where(u => window.TargetUnitIds.Contains(u.Id) && u.ArchivedAt == null)
                : db.Units.AsNoTracking().Where(u =>
                    levelsInWindow.Select(l => levelIdByCode[l]).Contains(u.LevelId) && u.ArchivedAt == null);

            var units = await unitsQuery.Select(u => new { u.Id, u.LevelId }).ToListAsync(cancellationToken);

            foreach (var unit in units)
            {
                var level = levelCodeById[unit.LevelId];
                var dueAt = window.DueAtFor(level, unit.Id);
                if (dueAt is null)
                {
                    continue;
                }

                if (now >= window.OpensAt)
                {
                    TryRecord(db, seen, window.Id, unit.Id, WindowJobEventType.Opened, string.Empty, now, ref created);
                }

                var settings = window.ReminderSettings;

                if (settings.RemindFiveDaysBefore && now >= dueAt.Value.AddDays(-5) && now < dueAt.Value)
                {
                    TryRecord(db, seen, window.Id, unit.Id, WindowJobEventType.Reminder, "5", now, ref created);
                }

                if (settings.RemindTwoDaysBefore && now >= dueAt.Value.AddDays(-2) && now < dueAt.Value)
                {
                    TryRecord(db, seen, window.Id, unit.Id, WindowJobEventType.Reminder, "2", now, ref created);
                }

                if (settings.RemindOnDueDay && now.Date == dueAt.Value.Date)
                {
                    TryRecord(db, seen, window.Id, unit.Id, WindowJobEventType.Reminder, "0", now, ref created);
                }

                if (settings.EscalateToParentAfterThreeDays && now >= dueAt.Value.AddDays(3))
                {
                    TryRecord(db, seen, window.Id, unit.Id, WindowJobEventType.Escalated, "Parent", now, ref created);
                }

                if (settings.EscalateToZoneAfterSevenDays && now >= dueAt.Value.AddDays(7))
                {
                    TryRecord(db, seen, window.Id, unit.Id, WindowJobEventType.Escalated, "Zone", now, ref created);
                }

                var status = window.StatusFor(level, now, unit.Id);
                if (status == WindowLevelStatus.Late)
                {
                    TryRecord(db, seen, window.Id, unit.Id, WindowJobEventType.Overdue, string.Empty, now, ref created);
                }
                else if (status == WindowLevelStatus.Closed)
                {
                    TryRecord(db, seen, window.Id, unit.Id, WindowJobEventType.Closed, string.Empty, now, ref created);
                }
            }
        }

        if (created > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return created;
    }

    private static void TryRecord(
        IAppDbContext db,
        HashSet<(Guid WindowId, Guid UnitId, WindowJobEventType EventType, string EventKey)> seen,
        Guid windowId,
        Guid unitId,
        WindowJobEventType eventType,
        string eventKey,
        DateTimeOffset now,
        ref int created)
    {
        if (!seen.Add((windowId, unitId, eventType, eventKey)))
        {
            return;
        }

        db.WindowJobRuns.Add(new WindowJobRun(Guid.CreateVersion7(), windowId, unitId, eventType, eventKey, now));
        created++;
    }
}
