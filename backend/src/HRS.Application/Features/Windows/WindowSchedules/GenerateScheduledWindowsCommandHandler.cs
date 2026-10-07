using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.WindowSchedules;

public sealed class GenerateScheduledWindowsCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GenerateScheduledWindowsCommand, Result<int>>
{
    public async ValueTask<Result<int>> Handle(GenerateScheduledWindowsCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsWindowAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure<int>(Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage submission windows."));
        }

        var schedule = await db.WindowSchedules.FirstOrDefaultAsync(
            s => s.Id == request.ScheduleId && s.ReportTypeId == request.ReportTypeId, cancellationToken);

        if (schedule is null)
        {
            return Result.Failure<int>(Error.NotFound("SCHEDULE_NOT_FOUND", "Window schedule not found."));
        }

        var windowsGenerated = await ScheduledWindowGenerator.GenerateAsync(db, schedule, clock.UtcNow, cancellationToken);

        return Result.Success(windowsGenerated);
    }
}
