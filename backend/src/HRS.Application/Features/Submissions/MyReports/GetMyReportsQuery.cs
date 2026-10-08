using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.MyReports;

/// <summary>"My reports" inbox (FR-SUB-001): every report due for the caller's units, across
/// every active UnitHead/Delegate assignment they hold — not just the unit they're "acting as"
/// right now (the mockup's own "My reports" row set spans more than one unit at once).</summary>
public sealed record GetMyReportsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<MyReportSummary>>>;

public sealed class GetMyReportsQueryHandler(IAppDbContext db, IClock clock)
    : IRequestHandler<GetMyReportsQuery, Result<IReadOnlyList<MyReportSummary>>>
{
    public async ValueTask<Result<IReadOnlyList<MyReportSummary>>> Handle(
        GetMyReportsQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var assignedUnitIds = await db.RoleAssignments
            .AsNoTracking()
            .Where(a => a.UserId == request.UserId
                && (a.Role == RoleName.UnitHead || a.Role == RoleName.Delegate)
                && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .Select(a => a.UnitId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (assignedUnitIds.Count == 0)
        {
            return Result.Success<IReadOnlyList<MyReportSummary>>([]);
        }

        var units = await db.Units
            .AsNoTracking()
            .Where(u => assignedUnitIds.Contains(u.Id) && u.ArchivedAt == null)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { Unit = u, LevelCode = l.Code })
            .ToListAsync(cancellationToken);

        if (units.Count == 0)
        {
            return Result.Success<IReadOnlyList<MyReportSummary>>([]);
        }

        var reportTypes = await db.ReportTypes.AsNoTracking().Where(r => r.ArchivedAt == null).ToListAsync(cancellationToken);
        var reportTypeById = reportTypes.ToDictionary(r => r.Id);
        var reportTypeIds = reportTypes.Select(r => r.Id).ToList();

        var windows = await db.SubmissionWindows
            .AsNoTracking()
            .Where(w => reportTypeIds.Contains(w.ReportTypeId))
            .ToListAsync(cancellationToken);

        var windowIds = windows.Select(w => w.Id).ToList();
        var unitIds = units.Select(entry => entry.Unit.Id).ToList();

        var instances = await db.ReportInstances
            .AsNoTracking()
            .Where(i => windowIds.Contains(i.WindowId) && unitIds.Contains(i.UnitId))
            .ToListAsync(cancellationToken);
        var instanceByKey = instances.ToDictionary(i => (i.WindowId, i.UnitId));

        // Everything below reads domain methods (StatusFor, DueAtFor) that aren't SQL-translatable
        // — same reason GetOrganizationTreeQuery materializes before calling IsActive().
        var results = new List<MyReportSummary>();

        foreach (var window in windows)
        {
            if (!reportTypeById.TryGetValue(window.ReportTypeId, out var reportType))
            {
                continue;
            }

            var levelsInWindow = window.DueDates.Select(d => d.Level).ToHashSet();
            if (levelsInWindow.Count == 0)
            {
                continue;
            }

            foreach (var entry in units)
            {
                var unit = entry.Unit;

                // Same scoping rule as WindowJobRunner (M4.4): a hand-picked TargetUnitIds list
                // narrows to exactly those units; otherwise every unit at an applicable level.
                var inScope = window.TargetUnitIds.Count > 0
                    ? window.TargetUnitIds.Contains(unit.Id)
                    : levelsInWindow.Contains(entry.LevelCode);

                if (!inScope)
                {
                    continue;
                }

                var dueAt = window.DueAtFor(entry.LevelCode, unit.Id);
                if (dueAt is null)
                {
                    continue;
                }

                instanceByKey.TryGetValue((window.Id, unit.Id), out var instance);

                var status = MyReportStatusCalculator.Determine(window, instance, entry.LevelCode, unit.Id, now);

                results.Add(new MyReportSummary(
                    instance?.Id,
                    window.Id,
                    reportType.Id,
                    reportType.Name,
                    unit.Id,
                    unit.Name,
                    window.PeriodLabel,
                    dueAt.Value,
                    instance?.SubmittedAt,
                    status));
            }
        }

        IReadOnlyList<MyReportSummary> sorted = results
            .OrderBy(r => r.DueAt)
            .ThenBy(r => r.ReportTypeName, StringComparer.Ordinal)
            .ToList();

        return Result.Success(sorted);
    }
}
