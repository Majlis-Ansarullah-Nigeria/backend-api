using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Templates.TemplateVersions;

public sealed record GetTemplateVersionsQuery(Guid ReportTypeId) : IRequest<Result<IReadOnlyList<TemplateVersionSummary>>>;

public sealed class GetTemplateVersionsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetTemplateVersionsQuery, Result<IReadOnlyList<TemplateVersionSummary>>>
{
    public async ValueTask<Result<IReadOnlyList<TemplateVersionSummary>>> Handle(
        GetTemplateVersionsQuery request, CancellationToken cancellationToken)
    {
        var reportTypeExists = await db.ReportTypes.AnyAsync(r => r.Id == request.ReportTypeId, cancellationToken);

        if (!reportTypeExists)
        {
            return Result.Failure<IReadOnlyList<TemplateVersionSummary>>(
                Error.NotFound("REPORT_TYPE_NOT_FOUND", "Report type not found."));
        }

        var versions = await db.TemplateVersions
            .AsNoTracking()
            .Where(v => v.ReportTypeId == request.ReportTypeId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(cancellationToken);

        IReadOnlyList<TemplateVersionSummary> summaries = versions.Select(TemplateVersionSummary.FromEntity).ToList();

        return Result.Success(summaries);
    }
}
