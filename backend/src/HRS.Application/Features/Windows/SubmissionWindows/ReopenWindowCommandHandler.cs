using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed class ReopenWindowCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IAuditLog auditLog)
    : IRequestHandler<ReopenWindowCommand, Result<SubmissionWindowSummary>>
{
    public async ValueTask<Result<SubmissionWindowSummary>> Handle(ReopenWindowCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsWindowAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure<SubmissionWindowSummary>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage submission windows."));
        }

        var window = await db.SubmissionWindows.FirstOrDefaultAsync(
            w => w.Id == request.WindowId && w.ReportTypeId == request.ReportTypeId, cancellationToken);

        if (window is null)
        {
            return Result.Failure<SubmissionWindowSummary>(Error.NotFound("WINDOW_NOT_FOUND", "Submission window not found."));
        }

        if (window.TargetUnitIds.Count > 0 && request.UnitIds.Except(window.TargetUnitIds).Any())
        {
            return Result.Failure<SubmissionWindowSummary>(Error.Validation(
                "UNIT_NOT_TARGETED_BY_WINDOW", "This window doesn't target one or more of the given units."));
        }

        var dueDates = window.DueDates.Select(d => new WindowDueDate(d.Level, d.DueAt)).ToList();
        var targetUnitError = await TargetUnitValidator.ValidateAsync(db, request.UnitIds, dueDates, cancellationToken);
        if (targetUnitError is not null)
        {
            return Result.Failure<SubmissionWindowSummary>(targetUnitError);
        }

        var now = clock.UtcNow;
        foreach (var unitId in request.UnitIds)
        {
            var effectiveClosesAt = window.EffectiveClosesAtFor(unitId);

            if (now < effectiveClosesAt)
            {
                return Result.Failure<SubmissionWindowSummary>(Error.Conflict(
                    "WINDOW_NOT_CLOSED", $"Unit {unitId} hasn't closed yet — grant an extension instead."));
            }

            if (request.ReopenedUntil <= effectiveClosesAt)
            {
                return Result.Failure<SubmissionWindowSummary>(Error.Validation(
                    "REOPEN_MUST_EXTEND_PAST_CLOSE", $"The new deadline must be after unit {unitId}'s current close date."));
            }
        }

        window.Reopen(request.UnitIds, request.ReopenedUntil, request.Reason, now);
        var unitNames = await db.Units.AsNoTracking().Where(u => request.UnitIds.Contains(u.Id)).Select(u => u.Name).ToListAsync(cancellationToken);
        auditLog.Add(new AuditEntry(
            AuditActions.WindowReopened, request.UserId, "window", window.Id.ToString(),
            $"{window.PeriodLabel} reopened for {string.Join(", ", unitNames)} until {request.ReopenedUntil:d MMM}",
            request.UnitIds.Count == 1 ? request.UnitIds[0] : null,
            After: new { Units = unitNames, request.ReopenedUntil, request.Reason }));
        await db.SaveChangesAsync(cancellationToken);

        return SubmissionWindowSummary.FromEntity(window);
    }
}
