using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.WindowSchedules;

public sealed class DeactivateWindowScheduleCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<DeactivateWindowScheduleCommand, Result>
{
    public async ValueTask<Result> Handle(DeactivateWindowScheduleCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsWindowAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage submission windows."));
        }

        var schedule = await db.WindowSchedules.FirstOrDefaultAsync(
            s => s.Id == request.ScheduleId && s.ReportTypeId == request.ReportTypeId, cancellationToken);

        if (schedule is null)
        {
            return Result.Failure(Error.NotFound("SCHEDULE_NOT_FOUND", "Window schedule not found."));
        }

        schedule.Deactivate();
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
