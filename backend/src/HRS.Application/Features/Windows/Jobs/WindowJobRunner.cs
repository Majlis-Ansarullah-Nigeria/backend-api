using System.Globalization;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Notifications;
using HRS.Application.Features.Organization.Units;
using HRS.Domain.Notifications;
using HRS.Domain.Organization;
using HRS.Domain.Windows;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.Jobs;

/// <summary>
/// Detects window-lifecycle transitions (BR-WIN-08: open, remind, overdue, close, escalate) and
/// logs each one exactly once per (window, unit); also queues the matching notification for four
/// of those five — Opened, Reminder, Overdue (FR-NOT-002) and Escalated (FR-NOT-005, M8.3; Closed
/// stays pure WindowJobRun bookkeeping). Still never touches a ReportInstance (M5.1's instance
/// lifecycle is the intended consumer of a unit's Closed event). Which reminder/escalation stages
/// actually fire is read per-window from WindowReminderSettings (the "Create window" mockup's
/// Reminders panel), not a fixed cadence. Escalation is scoped to Muqam-level units only, matching
/// FR-NOT-005's literal wording ("if a Muqam is...") — a window covering other levels too (its
/// DueDates can span several) never escalates for those.
/// </summary>
public static class WindowJobRunner
{
    private static readonly HashSet<WindowJobEventType> NotifiableEventTypes =
        [WindowJobEventType.Opened, WindowJobEventType.Reminder, WindowJobEventType.Overdue, WindowJobEventType.Escalated];

    public static async Task<int> RunAsync(
        IAppDbContext db, IClock clock, string publicUrl, CancellationToken cancellationToken, OrganizationBranding? branding = null)
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

        var reportTypeNameById = await db.ReportTypes.AsNoTracking()
            .Where(r => windows.Select(w => w.ReportTypeId).Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);

        // Only needed for the Zone-escalation case (ZoneResolver walks a unit's materialized
        // Path), but cheap enough to always load, same as GetInboxQuery's own zone resolution.
        var unitByPath = await db.Units.AsNoTracking().ToDictionaryAsync(u => u.Path, cancellationToken);

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

            var units = await unitsQuery.Select(u => new { u.Id, u.Name, u.LevelId, u.ParentId, u.Path }).ToListAsync(cancellationToken);
            var reportTypeName = reportTypeNameById.GetValueOrDefault(window.ReportTypeId, "Report");

            foreach (var unit in units)
            {
                var level = levelCodeById[unit.LevelId];
                var dueAt = window.DueAtFor(level, unit.Id);
                if (dueAt is null)
                {
                    continue;
                }

                var placeholders = new Dictionary<string, string>
                {
                    ["unitName"] = unit.Name,
                    ["reportTypeName"] = reportTypeName,
                    ["periodLabel"] = window.PeriodLabel,
                    ["dueDate"] = (branding ?? OrganizationBranding.Default).Date(dueAt.Value),
                    ["link"] = $"{publicUrl.TrimEnd('/')}/reports/{window.Id}/edit?unit={unit.Id}",
                };

                if (now >= window.OpensAt)
                {
                    created += await TryRecordAsync(
                        db, seen, window.Id, unit.Id, WindowJobEventType.Opened, string.Empty, placeholders, now, cancellationToken);
                }

                var settings = window.ReminderSettings;

                if (settings.RemindFiveDaysBefore && now >= dueAt.Value.AddDays(-5) && now < dueAt.Value)
                {
                    created += await TryRecordAsync(
                        db, seen, window.Id, unit.Id, WindowJobEventType.Reminder, "5", placeholders, now, cancellationToken);
                }

                if (settings.RemindTwoDaysBefore && now >= dueAt.Value.AddDays(-2) && now < dueAt.Value)
                {
                    created += await TryRecordAsync(
                        db, seen, window.Id, unit.Id, WindowJobEventType.Reminder, "2", placeholders, now, cancellationToken);
                }

                if (settings.RemindOnDueDay && now.Date == dueAt.Value.Date)
                {
                    created += await TryRecordAsync(
                        db, seen, window.Id, unit.Id, WindowJobEventType.Reminder, "0", placeholders, now, cancellationToken);
                }

                if (level == UnitLevelCode.Muqam && settings.EscalateToParentAfterThreeDays && now >= dueAt.Value.AddDays(3)
                    && unit.ParentId is { } dilaUnitId)
                {
                    created += await TryRecordAsync(
                        db, seen, window.Id, unit.Id, WindowJobEventType.Escalated, "Parent", placeholders, now, cancellationToken,
                        notifyUnitId: dilaUnitId);
                }

                if (level == UnitLevelCode.Muqam && settings.EscalateToZoneAfterSevenDays && now >= dueAt.Value.AddDays(7)
                    && ZoneResolver.Resolve(unit.Path, level, unitByPath).ZoneUnitId is { } zoneUnitId)
                {
                    created += await TryRecordAsync(
                        db, seen, window.Id, unit.Id, WindowJobEventType.Escalated, "Zone", placeholders, now, cancellationToken,
                        notifyUnitId: zoneUnitId);
                }

                var status = window.StatusFor(level, now, unit.Id);
                if (status == WindowLevelStatus.Late)
                {
                    created += await TryRecordAsync(
                        db, seen, window.Id, unit.Id, WindowJobEventType.Overdue, string.Empty, placeholders, now, cancellationToken);
                }
                else if (status == WindowLevelStatus.Closed)
                {
                    created += await TryRecordAsync(
                        db, seen, window.Id, unit.Id, WindowJobEventType.Closed, string.Empty, placeholders, now, cancellationToken);
                }
            }
        }

        if (created > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return created;
    }

    private static async Task<int> TryRecordAsync(
        IAppDbContext db,
        HashSet<(Guid WindowId, Guid UnitId, WindowJobEventType EventType, string EventKey)> seen,
        Guid windowId,
        Guid unitId,
        WindowJobEventType eventType,
        string eventKey,
        IReadOnlyDictionary<string, string> placeholders,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        Guid? notifyUnitId = null)
    {
        if (!seen.Add((windowId, unitId, eventType, eventKey)))
        {
            return 0;
        }

        // Bookkeeping always logs against the unit the event is actually about (the overdue
        // Muqam), even when the notification itself goes to a different unit's heads (escalation).
        db.WindowJobRuns.Add(new WindowJobRun(Guid.CreateVersion7(), windowId, unitId, eventType, eventKey, now));

        if (NotifiableEventTypes.Contains(eventType))
        {
            var notificationEventType = eventType switch
            {
                WindowJobEventType.Opened => NotificationEventType.WindowOpened,
                WindowJobEventType.Overdue => NotificationEventType.Overdue,
                WindowJobEventType.Escalated => NotificationEventType.Escalated,
                _ => NotificationEventType.Reminder,
            };

            await OutboxWriter.EnqueueForUnitHeadsAsync(db, notifyUnitId ?? unitId, notificationEventType, placeholders, now, cancellationToken);
        }

        return 1;
    }
}
