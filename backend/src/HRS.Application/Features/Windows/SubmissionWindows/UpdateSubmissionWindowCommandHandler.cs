using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Domain.Windows;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed class UpdateSubmissionWindowCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<UpdateSubmissionWindowCommand, Result<CreateSubmissionWindowResult>>
{
    public async ValueTask<Result<CreateSubmissionWindowResult>> Handle(
        UpdateSubmissionWindowCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsWindowAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure<CreateSubmissionWindowResult>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage submission windows."));
        }

        var window = await db.SubmissionWindows.FirstOrDefaultAsync(
            w => w.Id == request.WindowId && w.ReportTypeId == request.ReportTypeId, cancellationToken);

        if (window is null)
        {
            return Result.Failure<CreateSubmissionWindowResult>(Error.NotFound("WINDOW_NOT_FOUND", "Submission window not found."));
        }

        var reportType = await db.ReportTypes.AsNoTracking().FirstAsync(r => r.Id == request.ReportTypeId, cancellationToken);

        var invalidLevel = request.DueDates.FirstOrDefault(d => !reportType.ApplicableLevels.Contains(d.Level));
        if (invalidLevel is not null)
        {
            return Result.Failure<CreateSubmissionWindowResult>(Error.Validation(
                "LEVEL_NOT_APPLICABLE", $"{invalidLevel.Level} doesn't submit this report type."));
        }

        window.Update(
            request.PeriodLabel,
            request.OpensAt,
            request.GraceDays,
            request.ClosesAt,
            request.DueDates.Select(d => new WindowLevelDueDate(d.Level, d.DueAt)));

        await db.SaveChangesAsync(cancellationToken);

        return new CreateSubmissionWindowResult(SubmissionWindowSummary.FromEntity(window), ParentBeforeChildWarnings.Detect(request.DueDates));
    }
}
