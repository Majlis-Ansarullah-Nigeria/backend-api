using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using HRS.Domain.Windows;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.Review;

/// <summary>FR-SUB-009: "The head MAY withdraw it while it is still unread by the recipient and
/// the window is open." Submitted is itself the "unread" signal (ReportInstance.Open's own doc
/// comment — there's no separate IsRead flag): once the Secretariat opens it, status moves to
/// InReview and Withdraw's own status guard already rejects it.</summary>
public sealed class WithdrawReportCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IAuditLog auditLog)
    : IRequestHandler<WithdrawReportCommand, Result>
{
    public async ValueTask<Result> Handle(WithdrawReportCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.CanSubmitReportForUnitAsync(request.UserId, request.UnitId, cancellationToken))
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "You don't have permission to withdraw this unit's reports."));
        }

        var instance = await db.ReportInstances.FirstOrDefaultAsync(i => i.Id == request.ReportId && i.UnitId == request.UnitId, cancellationToken);
        instance ??= await db.ReportInstances.FirstOrDefaultAsync(i => i.WindowId == request.ReportId && i.UnitId == request.UnitId, cancellationToken);

        if (instance is null)
        {
            return Result.Failure(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        if (instance.Status != ReportInstanceStatus.Submitted)
        {
            return Result.Failure(Error.Conflict("CANNOT_WITHDRAW", "Only a submitted, unread report can be withdrawn."));
        }

        var window = await db.SubmissionWindows.AsNoTracking().FirstAsync(w => w.Id == instance.WindowId, cancellationToken);
        var unitRow = await db.Units.AsNoTracking()
            .Where(u => u.Id == request.UnitId)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => l.Code)
            .FirstAsync(cancellationToken);

        var now = clock.UtcNow;
        if (window.StatusFor(unitRow, now, request.UnitId) == WindowLevelStatus.Closed)
        {
            return Result.Failure(Error.Conflict("WINDOW_CLOSED", "This window has closed."));
        }

        instance.Withdraw();
        db.ReportRevisions.Add(instance.RecordWithdrawal(request.UserId, now));
        auditLog.Add(new AuditEntry(
            AuditActions.ReportWithdrawn, request.UserId, "report", instance.Id.ToString(),
            await AuditDescriptions.ReportAsync(db, instance.WindowId, instance.UnitId, cancellationToken), instance.UnitId));
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
