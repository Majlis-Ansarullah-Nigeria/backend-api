using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.WindowSchedules;

public sealed record GetWindowScheduleQuery(Guid ReportTypeId, Guid ScheduleId) : IRequest<Result<WindowScheduleSummary>>;

public sealed class GetWindowScheduleQueryHandler(IAppDbContext db)
    : IRequestHandler<GetWindowScheduleQuery, Result<WindowScheduleSummary>>
{
    public async ValueTask<Result<WindowScheduleSummary>> Handle(GetWindowScheduleQuery request, CancellationToken cancellationToken)
    {
        var schedule = await db.WindowSchedules.AsNoTracking().FirstOrDefaultAsync(
            s => s.Id == request.ScheduleId && s.ReportTypeId == request.ReportTypeId, cancellationToken);

        return schedule is null
            ? Result.Failure<WindowScheduleSummary>(Error.NotFound("SCHEDULE_NOT_FOUND", "Window schedule not found."))
            : WindowScheduleSummary.FromEntity(schedule);
    }
}
