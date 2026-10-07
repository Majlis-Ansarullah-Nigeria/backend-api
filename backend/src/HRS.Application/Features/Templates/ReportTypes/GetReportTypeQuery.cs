using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Templates.ReportTypes;

public sealed record GetReportTypeQuery(Guid ReportTypeId) : IRequest<Result<ReportTypeSummary>>;

public sealed class GetReportTypeQueryHandler(IAppDbContext db)
    : IRequestHandler<GetReportTypeQuery, Result<ReportTypeSummary>>
{
    public async ValueTask<Result<ReportTypeSummary>> Handle(GetReportTypeQuery request, CancellationToken cancellationToken)
    {
        var reportType = await db.ReportTypes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.ReportTypeId, cancellationToken);

        return reportType is null
            ? Result.Failure<ReportTypeSummary>(Error.NotFound("REPORT_TYPE_NOT_FOUND", "Report type not found."))
            : ReportTypeSummary.FromEntity(reportType);
    }
}
