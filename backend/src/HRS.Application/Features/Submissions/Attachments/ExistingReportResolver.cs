using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.ReportInstances;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.Attachments;

/// <summary>Shared by CompleteAttachmentUpload and DeleteAttachment — both act on an attachment
/// that, by construction, can only exist once a ReportInstance already does (an attachment row
/// is only ever created inside PresignAttachmentUpload, which creates the instance first). The
/// same dual-mode id resolution as Get/SaveReportDraft (id is either a real instance id or,
/// transiently, the window id) still applies, since the frontend keeps using whichever id it
/// started the screen with.</summary>
internal static class ExistingReportResolver
{
    public static async Task<Result<ReportInstance>> ResolveAsync(
        IAppDbContext db, Guid reportId, Guid unitId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var instance = await db.ReportInstances.FirstOrDefaultAsync(i => i.Id == reportId && i.UnitId == unitId, cancellationToken);

        // reportId didn't match an instance directly — it may be the window id the frontend
        // started the screen with, from before any instance existed (same fallback as Get/Save).
        if (instance is null)
        {
            instance = await db.ReportInstances.FirstOrDefaultAsync(i => i.WindowId == reportId && i.UnitId == unitId, cancellationToken);
        }

        if (instance is null)
        {
            return Result.Failure<ReportInstance>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        var window = await db.SubmissionWindows.AsNoTracking().FirstOrDefaultAsync(w => w.Id == instance.WindowId, cancellationToken);
        if (window is null)
        {
            return Result.Failure<ReportInstance>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        var unitRow = await db.Units.AsNoTracking()
            .Where(u => u.Id == unitId)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { l.Code })
            .FirstOrDefaultAsync(cancellationToken);
        if (unitRow is null)
        {
            return Result.Failure<ReportInstance>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        var editError = ReportEditability.Check(window, instance, unitRow.Code, unitId, now);
        return editError is not null ? Result.Failure<ReportInstance>(editError) : instance;
    }
}
