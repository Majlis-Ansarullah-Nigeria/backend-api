using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed record GetSubmissionWindowsQuery(Guid ReportTypeId) : IRequest<Result<IReadOnlyList<SubmissionWindowSummary>>>;

public sealed class GetSubmissionWindowsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetSubmissionWindowsQuery, Result<IReadOnlyList<SubmissionWindowSummary>>>
{
    public async ValueTask<Result<IReadOnlyList<SubmissionWindowSummary>>> Handle(
        GetSubmissionWindowsQuery request, CancellationToken cancellationToken)
    {
        var reportTypeExists = await db.ReportTypes.AnyAsync(r => r.Id == request.ReportTypeId, cancellationToken);

        if (!reportTypeExists)
        {
            return Result.Failure<IReadOnlyList<SubmissionWindowSummary>>(
                Error.NotFound("REPORT_TYPE_NOT_FOUND", "Report type not found."));
        }

        var windows = await db.SubmissionWindows
            .AsNoTracking()
            .Where(w => w.ReportTypeId == request.ReportTypeId)
            .OrderByDescending(w => w.OpensAt)
            .ToListAsync(cancellationToken);

        IReadOnlyList<SubmissionWindowSummary> summaries = windows.Select(SubmissionWindowSummary.FromEntity).ToList();

        return Result.Success(summaries);
    }
}
