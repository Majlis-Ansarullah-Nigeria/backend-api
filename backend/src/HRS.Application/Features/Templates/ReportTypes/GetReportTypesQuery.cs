using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Templates.ReportTypes;

public sealed record GetReportTypesQuery(bool IncludeArchived) : IRequest<Result<IReadOnlyList<ReportTypeSummary>>>;

public sealed class GetReportTypesQueryHandler(IAppDbContext db)
    : IRequestHandler<GetReportTypesQuery, Result<IReadOnlyList<ReportTypeSummary>>>
{
    public async ValueTask<Result<IReadOnlyList<ReportTypeSummary>>> Handle(
        GetReportTypesQuery request, CancellationToken cancellationToken)
    {
        var query = db.ReportTypes.AsNoTracking().AsQueryable();

        if (!request.IncludeArchived)
        {
            query = query.Where(r => r.ArchivedAt == null);
        }

        var reportTypes = await query.OrderBy(r => r.Name).ToListAsync(cancellationToken);

        IReadOnlyList<ReportTypeSummary> summaries = reportTypes.Select(ReportTypeSummary.FromEntity).ToList();

        return Result.Success(summaries);
    }
}
