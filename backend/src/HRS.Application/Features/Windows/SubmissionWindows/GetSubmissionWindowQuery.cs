using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed record GetSubmissionWindowQuery(Guid ReportTypeId, Guid WindowId) : IRequest<Result<SubmissionWindowSummary>>;

public sealed class GetSubmissionWindowQueryHandler(IAppDbContext db)
    : IRequestHandler<GetSubmissionWindowQuery, Result<SubmissionWindowSummary>>
{
    public async ValueTask<Result<SubmissionWindowSummary>> Handle(GetSubmissionWindowQuery request, CancellationToken cancellationToken)
    {
        var window = await db.SubmissionWindows.AsNoTracking().FirstOrDefaultAsync(
            w => w.Id == request.WindowId && w.ReportTypeId == request.ReportTypeId, cancellationToken);

        return window is null
            ? Result.Failure<SubmissionWindowSummary>(Error.NotFound("WINDOW_NOT_FOUND", "Submission window not found."))
            : SubmissionWindowSummary.FromEntity(window);
    }
}
