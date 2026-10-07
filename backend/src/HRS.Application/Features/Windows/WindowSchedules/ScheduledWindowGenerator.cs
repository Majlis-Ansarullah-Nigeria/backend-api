using HRS.Application.Common.Interfaces;
using HRS.Domain.Windows;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.WindowSchedules;

/// <summary>
/// Generates up to 12 months (or 4 quarters / 1 year) of SubmissionWindows from a schedule
/// (FR-WIN-002), idempotently — a period that already has a window for this report type is
/// skipped, so calling this again (on update, or on demand) only tops up what's missing rather
/// than duplicating. There's no separate cron job for this (see M4.2's commit notes): it runs
/// synchronously whenever a schedule is created or updated.
/// </summary>
public static class ScheduledWindowGenerator
{
    public static async Task<int> GenerateAsync(IAppDbContext db, WindowSchedule schedule, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!schedule.IsActive)
        {
            return 0;
        }

        var existingLabels = await db.SubmissionWindows
            .AsNoTracking()
            .Where(w => w.ReportTypeId == schedule.ReportTypeId)
            .Select(w => w.PeriodLabel)
            .ToListAsync(cancellationToken);
        var existingLabelSet = existingLabels.ToHashSet();

        var periodsAhead = PeriodCalculator.PeriodsAheadFor(schedule.Frequency);
        var created = 0;

        for (var offset = 0; offset < periodsAhead; offset++)
        {
            var (label, periodEnd) = PeriodCalculator.GetPeriod(schedule.Frequency, now, offset);

            if (existingLabelSet.Contains(label))
            {
                continue;
            }

            var (opensAt, graceDays, closesAt, dueDates) = schedule.BuildWindow(periodEnd);

            var window = SubmissionWindow.Create(
                Guid.CreateVersion7(),
                schedule.ReportTypeId,
                label,
                opensAt,
                graceDays,
                closesAt,
                dueDates,
                now,
                scheduleId: schedule.Id);

            db.SubmissionWindows.Add(window);
            created++;
        }

        if (created > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return created;
    }
}
