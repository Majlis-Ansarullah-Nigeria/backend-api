using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Domain.Windows;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.WindowSchedules;

public sealed class UpdateWindowScheduleCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<UpdateWindowScheduleCommand, Result<CreateWindowScheduleResult>>
{
    public async ValueTask<Result<CreateWindowScheduleResult>> Handle(
        UpdateWindowScheduleCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsWindowAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure<CreateWindowScheduleResult>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage submission windows."));
        }

        var schedule = await db.WindowSchedules.FirstOrDefaultAsync(
            s => s.Id == request.ScheduleId && s.ReportTypeId == request.ReportTypeId, cancellationToken);

        if (schedule is null)
        {
            return Result.Failure<CreateWindowScheduleResult>(Error.NotFound("SCHEDULE_NOT_FOUND", "Window schedule not found."));
        }

        var reportType = await db.ReportTypes.AsNoTracking().FirstAsync(r => r.Id == request.ReportTypeId, cancellationToken);

        var invalidLevel = request.DueOffsets.FirstOrDefault(o => !reportType.ApplicableLevels.Contains(o.Level));
        if (invalidLevel is not null)
        {
            return Result.Failure<CreateWindowScheduleResult>(Error.Validation(
                "LEVEL_NOT_APPLICABLE", $"{invalidLevel.Level} doesn't submit this report type."));
        }

        schedule.Update(
            request.Frequency,
            request.OpensDayOffset,
            request.GraceDays,
            request.ClosesDayOffset,
            request.DueOffsets.Select(o => new ScheduleLevelDueOffset(o.Level, o.DueDayOffset)));

        await db.SaveChangesAsync(cancellationToken);

        var windowsGenerated = await ScheduledWindowGenerator.GenerateAsync(db, schedule, clock.UtcNow, cancellationToken);

        return new CreateWindowScheduleResult(WindowScheduleSummary.FromEntity(schedule), windowsGenerated);
    }
}
