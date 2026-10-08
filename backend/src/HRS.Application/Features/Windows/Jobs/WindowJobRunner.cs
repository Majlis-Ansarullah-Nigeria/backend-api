using System.Globalization;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Organization;
using HRS.Domain.Windows;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.Jobs;

/// <summary>
/// Detects window-lifecycle transitions (BR-WIN-08: open, remind, overdue, close) and logs each
/// one exactly once per (window, unit). Pure detection — it never sends a notification itself and
/// never touches a ReportInstance (M5.1 doesn't exist yet, so there is nothing to actually mark
/// Missed); M8's outbox dispatcher is the intended consumer of the rows this writes, and M5.1's
/// instance lifecycle is the intended consumer of a unit's Closed event once it exists.
/// </summary>
public static class WindowJobRunner
{
    // FR-NOT-002's example cadence. Real per-organization configurability is FR-NOT-003 (M8) —
    // hardcoded here deliberately, not a shortcut to revisit later without a reason to.
    private static readonly int[] ReminderDaysBeforeDue = [5, 2, 1];

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

                foreach (var days in ReminderDaysBeforeDue)
                {
                    var remindAt = dueAt.Value.AddDays(-days);
                    if (now >= remindAt && now < dueAt.Value)
                    {
                        TryRecord(
                            db, seen, window.Id, unit.Id, WindowJobEventType.Reminder,
                            days.ToString(CultureInfo.InvariantCulture), now, ref created);
                    }
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
