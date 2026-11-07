using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Application.Common.Models;
using ManagementApi.Application.Reports.Common;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Commands;

/// <summary>
/// Marks reports Missed once a unit's close date (after any extension) has passed:
/// drafts become Missed, and units that never started get a Missed record. Safe to re-run.
/// </summary>
public record MarkMissedReportsCommand : IRequest<Result<int>>;

/// <summary>
/// Reminds the heads of units that have not submitted, 3 days and 1 day before their due date and on the day.
/// Each reminder is sent once per unit (logged in ReportReminders). Safe to re-run.
/// </summary>
public record SendDueRemindersCommand : IRequest<Result<int>>;

public class ReportWindowJobHandlers :
    IRequestHandler<MarkMissedReportsCommand, Result<int>>,
    IRequestHandler<SendDueRemindersCommand, Result<int>>
{
    private static readonly int[] ReminderDays = { 3, 1, 0 };

    // Windows closed longer ago than this are not rescanned; extensions are rarely longer.
    private static readonly TimeSpan MissedLookback = TimeSpan.FromDays(60);

    private readonly IApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly TimeProvider _clock;

    public ReportWindowJobHandlers(IApplicationDbContext context, INotificationService notificationService, TimeProvider clock)
    {
        _context = context;
        _notificationService = notificationService;
        _clock = clock;
    }

    public async Task<Result<int>> Handle(MarkMissedReportsCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var since = now - MissedLookback;

        var windows = await _context.SubmissionWindows
            .Include(w => w.ReportTemplate)
            .Include(w => w.Extensions)
            .Where(w => w.IsActive && w.EndDate < now && w.EndDate > since)
            .ToListAsync(cancellationToken);

        var marked = 0;
        foreach (var window in windows)
        {
            var units = await ExpectedUnits.ForLevelsAsync(_context, window.ReportTemplate.SubmittingLevels, cancellationToken);
            var reports = await _context.ReportSubmissions
                .Where(s => s.SubmissionWindowId == window.Id)
                .ToDictionaryAsync(s => (s.OrganizationLevel, s.UnitId), cancellationToken);

            foreach (var unit in units)
            {
                if (now <= window.ClosesAtFor(unit.Level, unit.UnitId))
                {
                    continue; // this unit still has time (extension)
                }

                if (reports.TryGetValue((unit.Level, unit.UnitId), out var report))
                {
                    if (report.Status == SubmissionStatus.Draft)
                    {
                        report.MarkMissed();
                        marked++;
                    }
                }
                else
                {
                    _context.ReportSubmissions.Add(ReportSubmission.CreateMissed(
                        window.ReportTemplateId, window.Id, unit.Level, unit.UnitId, unit.MuqamId, unit.DilaId, unit.ZoneId));
                    marked++;
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(marked, $"{marked} report(s) marked missed");
    }

    public async Task<Result<int>> Handle(SendDueRemindersCommand request, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        var windows = await _context.SubmissionWindows
            .Include(w => w.ReportTemplate)
            .Include(w => w.Extensions)
            .Where(w => w.IsActive && w.StartDate <= now && w.EndDate.AddDays(60) >= now)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var window in windows)
        foreach (var level in window.ReportTemplate.SubmittingLevels.Where(ExpectedUnits.IsUnitLevel))
        {
            var units = await ExpectedUnits.ForLevelAsync(_context, level, cancellationToken);

            var notPending = await _context.ReportSubmissions
                .Where(s => s.SubmissionWindowId == window.Id && s.OrganizationLevel == level && s.Status != SubmissionStatus.Draft)
                .Select(s => s.UnitId)
                .ToListAsync(cancellationToken);
            var alreadySent = await _context.ReportReminders
                .Where(r => r.SubmissionWindowId == window.Id && r.OrganizationLevel == level)
                .Select(r => new { r.UnitId, r.DaysBeforeDue })
                .ToListAsync(cancellationToken);
            var sentKeys = alreadySent.Select(r => (r.UnitId, r.DaysBeforeDue)).ToHashSet();
            var done = notPending.ToHashSet();

            var due = units
                .Where(u => !done.Contains(u.UnitId) && window.IsOpenFor(now, level, u.UnitId))
                .Select(u => (Unit: u, DueAt: window.DueAtFor(level, u.UnitId)))
                .Select(x => (x.Unit, x.DueAt, Days: (x.DueAt.Date - now.Date).Days))
                .Where(x => ReminderDays.Contains(x.Days) && !sentKeys.Contains((x.Unit.UnitId, x.Days)))
                .ToList();

            if (due.Count == 0)
            {
                continue;
            }

            var unitIds = due.Select(d => d.Unit.UnitId).ToList();
            var heads = await _context.Users
                .Where(u => u.IsActive && u.OrganizationLevel == level)
                .Where(u => (level == OrganizationLevel.Muqam && u.MuqamId != null && unitIds.Contains(u.MuqamId.Value)) ||
                            (level == OrganizationLevel.Dila && u.DilaId != null && unitIds.Contains(u.DilaId.Value)) ||
                            (level == OrganizationLevel.Zone && u.ZoneId != null && unitIds.Contains(u.ZoneId.Value)))
                .Select(u => new { u.Id, u.UserName, u.FirstName, u.LastName, u.MuqamId, u.DilaId, u.ZoneId })
                .ToListAsync(cancellationToken);

            foreach (var group in due.GroupBy(d => (d.DueAt, d.Days)))
            {
                var groupUnits = group.Select(g => g.Unit.UnitId).ToHashSet();
                var recipients = heads
                    .Where(h => groupUnits.Contains((level switch
                    {
                        OrganizationLevel.Muqam => h.MuqamId,
                        OrganizationLevel.Dila => h.DilaId,
                        _ => h.ZoneId
                    })!.Value))
                    .Select(h => (h.Id, $"{h.FirstName} {h.LastName}".Trim() is { Length: > 0 } name ? name : h.UserName ?? "Head"))
                    .ToList();

                if (recipients.Count > 0)
                {
                    try
                    {
                        await _notificationService.NotifyDeadlineApproachingAsync(
                            window.Id, window.Name, group.Key.DueAt, group.Key.Days, recipients, cancellationToken);
                        sent += recipients.Count;
                    }
                    catch
                    {
                        continue; // not logged, so the next run tries again
                    }
                }

                foreach (var item in group)
                {
                    _context.ReportReminders.Add(new ReportReminder(window.Id, level, item.Unit.UnitId, item.Days, now));
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(sent, $"{sent} reminder(s) sent");
    }
}
