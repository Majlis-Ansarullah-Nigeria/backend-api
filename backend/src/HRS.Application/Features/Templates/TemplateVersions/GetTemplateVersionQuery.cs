using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Templates.TemplateVersions;

public sealed record GetTemplateVersionQuery(Guid ReportTypeId, Guid VersionId) : IRequest<Result<TemplateVersionDetail>>;

public sealed class GetTemplateVersionQueryHandler(IAppDbContext db)
    : IRequestHandler<GetTemplateVersionQuery, Result<TemplateVersionDetail>>
{
    public async ValueTask<Result<TemplateVersionDetail>> Handle(GetTemplateVersionQuery request, CancellationToken cancellationToken)
    {
        var version = await db.TemplateVersions.AsNoTracking().FirstOrDefaultAsync(
            v => v.Id == request.VersionId && v.ReportTypeId == request.ReportTypeId, cancellationToken);

        return version is null
            ? Result.Failure<TemplateVersionDetail>(Error.NotFound("TEMPLATE_VERSION_NOT_FOUND", "Template version not found."))
            : TemplateVersionDetail.FromEntity(version);
    }
}
