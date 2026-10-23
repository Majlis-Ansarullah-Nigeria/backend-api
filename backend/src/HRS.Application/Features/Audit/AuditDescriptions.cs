using HRS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Audit;

/// <summary>The "Report type · Period · Unit" line most report-related audit entries carry, as in
/// the Audit log mockup ("General Monthly · Sep · Agodi Muqam").</summary>
public static class AuditDescriptions
{
    public static async Task<string> ReportAsync(IAppDbContext db, Guid windowId, Guid unitId, CancellationToken cancellationToken)
    {
        var report = await db.SubmissionWindows.AsNoTracking()
            .Where(w => w.Id == windowId)
            .Join(db.ReportTypes.AsNoTracking(), w => w.ReportTypeId, r => r.Id, (w, r) => new { r.Name, w.PeriodLabel })
            .FirstOrDefaultAsync(cancellationToken);
        var unitName = await db.Units.AsNoTracking().Where(u => u.Id == unitId).Select(u => u.Name).FirstOrDefaultAsync(cancellationToken);
        return string.Join(" · ", new[] { report?.Name, report?.PeriodLabel, unitName }.Where(part => !string.IsNullOrEmpty(part)));
    }

    public static async Task<string> WindowAsync(IAppDbContext db, Guid windowId, CancellationToken cancellationToken)
    {
        var window = await db.SubmissionWindows.AsNoTracking()
            .Where(w => w.Id == windowId)
            .Join(db.ReportTypes.AsNoTracking(), w => w.ReportTypeId, r => r.Id, (w, r) => new { r.Name, w.PeriodLabel })
            .FirstOrDefaultAsync(cancellationToken);
        return window is null ? windowId.ToString() : $"{window.Name} · {window.PeriodLabel}";
    }
}
