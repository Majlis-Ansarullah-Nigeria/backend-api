using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.WindowSchedules;

public sealed class ActivateWindowScheduleCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<ActivateWindowScheduleCommand, Result<CreateWindowScheduleResult>>
{
    public async ValueTask<Result<CreateWindowScheduleResult>> Handle(ActivateWindowScheduleCommand request, CancellationToken cancellationToken)
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

        schedule.Activate();
        await db.SaveChangesAsync(cancellationToken);

        var windowsGenerated = await ScheduledWindowGenerator.GenerateAsync(db, schedule, clock.UtcNow, cancellationToken);

        return new CreateWindowScheduleResult(WindowScheduleSummary.FromEntity(schedule), windowsGenerated);
    }
}
