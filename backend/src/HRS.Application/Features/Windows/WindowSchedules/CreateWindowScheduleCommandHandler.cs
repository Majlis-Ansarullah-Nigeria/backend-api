using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Domain.Windows;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.WindowSchedules;

public sealed class CreateWindowScheduleCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<CreateWindowScheduleCommand, Result<CreateWindowScheduleResult>>
{
    public async ValueTask<Result<CreateWindowScheduleResult>> Handle(
        CreateWindowScheduleCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsWindowAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure<CreateWindowScheduleResult>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage submission windows."));
        }

        var reportType = await db.ReportTypes.FirstOrDefaultAsync(r => r.Id == request.ReportTypeId, cancellationToken);

        if (reportType is null)
        {
            return Result.Failure<CreateWindowScheduleResult>(Error.NotFound("REPORT_TYPE_NOT_FOUND", "Report type not found."));
        }

        if (reportType.IsArchived)
        {
            return Result.Failure<CreateWindowScheduleResult>(
                Error.Conflict("REPORT_TYPE_ARCHIVED", "Can't create a schedule for an archived report type."));
        }

        var invalidLevel = request.DueOffsets.FirstOrDefault(o => !reportType.ApplicableLevels.Contains(o.Level));
        if (invalidLevel is not null)
        {
            return Result.Failure<CreateWindowScheduleResult>(Error.Validation(
                "LEVEL_NOT_APPLICABLE", $"{invalidLevel.Level} doesn't submit this report type."));
        }

        var now = clock.UtcNow;

        var schedule = WindowSchedule.Create(
            Guid.CreateVersion7(),
            request.ReportTypeId,
            request.Frequency,
            request.OpensDayOffset,
            request.GraceDays,
            request.ClosesDayOffset,
            request.DueOffsets.Select(o => new ScheduleLevelDueOffset(o.Level, o.DueDayOffset)),
            now);

        db.WindowSchedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);

        var windowsGenerated = await ScheduledWindowGenerator.GenerateAsync(db, schedule, now, cancellationToken);

        return new CreateWindowScheduleResult(WindowScheduleSummary.FromEntity(schedule), windowsGenerated);
    }
}
