using HRS.Application.Common.Interfaces;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using HRS.Domain.Windows;
using Microsoft.EntityFrameworkCore;
using Unit = HRS.Domain.Organization.Unit;

namespace HRS.Application.Features.Submissions.PullFigures;

/// <summary>
/// Shared resolution for GetPullContextQuery and PullChildFiguresCommand (FR-SUB-007) — same
/// reason ChildReportResolver (M6.2) and ExistingReportResolver (M5.4) exist. Returns null for any
/// reason pulling isn't possible here (not eligible, or the ids don't resolve to anything) —
/// callers that need to distinguish "forbidden" from "not eligible" do that access check first,
/// separately, since both handlers already gate on CanEditReportForUnitAsync before calling this.
/// </summary>
internal static class PullFiguresResolver
{
    public sealed record Resolved(
        SubmissionWindow Window,
        ReportType ReportType,
        UnitLevel ChildLevel,
        IReadOnlyList<Unit> Children,
        ReportInstance? Instance);

    public static async Task<Resolved?> ResolveAsync(IAppDbContext db, Guid unitId, Guid reportId, CancellationToken cancellationToken)
    {
        var unitLevel = await db.Units
            .AsNoTracking()
            .Where(u => u.Id == unitId)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => l.Code)
            .Cast<UnitLevelCode?>()
            .FirstOrDefaultAsync(cancellationToken);

        if (unitLevel is null)
        {
            return null;
        }

        var instance = await db.ReportInstances.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == reportId && i.UnitId == unitId, cancellationToken);
        instance ??= await db.ReportInstances.AsNoTracking()
            .FirstOrDefaultAsync(i => i.WindowId == reportId && i.UnitId == unitId, cancellationToken);

        // No instance yet: reportId can only be the window id (the lazy-creation pattern every
        // other Submissions handler already uses — Save, Presign, Submit).
        var windowId = instance?.WindowId ?? reportId;
        var window = await db.SubmissionWindows.AsNoTracking().FirstOrDefaultAsync(w => w.Id == windowId, cancellationToken);
        if (window is null)
        {
            return null;
        }

        var reportType = await db.ReportTypes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == window.ReportTypeId, cancellationToken);
        if (reportType is null || !reportType.ApplicableLevels.Contains(unitLevel.Value))
        {
            return null;
        }

        var children = await db.Units
            .AsNoTracking()
            .Where(u => u.ParentId == unitId && u.ArchivedAt == null)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { Unit = u, Level = l })
            .ToListAsync(cancellationToken);

        if (children.Count == 0 || !reportType.ApplicableLevels.Contains(children[0].Level.Code))
        {
            return null;
        }

        return new Resolved(window, reportType, children[0].Level, [.. children.Select(c => c.Unit)], instance);
    }
}
