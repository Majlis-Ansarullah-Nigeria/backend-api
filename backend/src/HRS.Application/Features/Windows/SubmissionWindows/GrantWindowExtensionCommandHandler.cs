using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed class GrantWindowExtensionCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GrantWindowExtensionCommand, Result<SubmissionWindowSummary>>
{
    public async ValueTask<Result<SubmissionWindowSummary>> Handle(GrantWindowExtensionCommand request, CancellationToken cancellationToken)
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

        window.GrantExtension(request.UnitIds, request.NewDueAt, request.NewClosesAt, request.Reason, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        return SubmissionWindowSummary.FromEntity(window);
    }
}
