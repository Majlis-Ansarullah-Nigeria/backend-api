using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.WindowSchedules;

public sealed record GetWindowSchedulesQuery(Guid ReportTypeId) : IRequest<Result<IReadOnlyList<WindowScheduleSummary>>>;

public sealed class GetWindowSchedulesQueryHandler(IAppDbContext db)
    : IRequestHandler<GetWindowSchedulesQuery, Result<IReadOnlyList<WindowScheduleSummary>>>
{
    public async ValueTask<Result<IReadOnlyList<WindowScheduleSummary>>> Handle(
        GetWindowSchedulesQuery request, CancellationToken cancellationToken)
    {
        var reportTypeExists = await db.ReportTypes.AnyAsync(r => r.Id == request.ReportTypeId, cancellationToken);

        if (!reportTypeExists)
        {
            return Result.Failure<IReadOnlyList<WindowScheduleSummary>>(
                Error.NotFound("REPORT_TYPE_NOT_FOUND", "Report type not found."));
        }

        var schedules = await db.WindowSchedules
            .AsNoTracking()
            .Where(s => s.ReportTypeId == request.ReportTypeId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        IReadOnlyList<WindowScheduleSummary> summaries = schedules.Select(WindowScheduleSummary.FromEntity).ToList();

        return Result.Success(summaries);
    }
}
