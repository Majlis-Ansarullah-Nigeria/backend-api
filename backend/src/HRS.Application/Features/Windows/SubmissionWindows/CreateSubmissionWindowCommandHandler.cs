using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Domain.Windows;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed class CreateSubmissionWindowCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<CreateSubmissionWindowCommand, Result<CreateSubmissionWindowResult>>
{
    public async ValueTask<Result<CreateSubmissionWindowResult>> Handle(
        CreateSubmissionWindowCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsWindowAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure<CreateSubmissionWindowResult>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage submission windows."));
        }

        var reportType = await db.ReportTypes.FirstOrDefaultAsync(r => r.Id == request.ReportTypeId, cancellationToken);

        if (reportType is null)
        {
            return Result.Failure<CreateSubmissionWindowResult>(Error.NotFound("REPORT_TYPE_NOT_FOUND", "Report type not found."));
        }

        if (reportType.IsArchived)
        {
            return Result.Failure<CreateSubmissionWindowResult>(
                Error.Conflict("REPORT_TYPE_ARCHIVED", "Can't create a window for an archived report type."));
        }

        var invalidLevel = request.DueDates.FirstOrDefault(d => !reportType.ApplicableLevels.Contains(d.Level));
        if (invalidLevel is not null)
        {
            return Result.Failure<CreateSubmissionWindowResult>(Error.Validation(
                "LEVEL_NOT_APPLICABLE", $"{invalidLevel.Level} doesn't submit this report type."));
        }

        var window = SubmissionWindow.Create(
            Guid.CreateVersion7(),
            request.ReportTypeId,
            request.PeriodLabel,
            request.OpensAt,
            request.GraceDays,
            request.ClosesAt,
            request.DueDates.Select(d => new WindowLevelDueDate(d.Level, d.DueAt)),
            clock.UtcNow);

        db.SubmissionWindows.Add(window);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateSubmissionWindowResult(SubmissionWindowSummary.FromEntity(window), ParentBeforeChildWarnings.Detect(request.DueDates));
    }
}
