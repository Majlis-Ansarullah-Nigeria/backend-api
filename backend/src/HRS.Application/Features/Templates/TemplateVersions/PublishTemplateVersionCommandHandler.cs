using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Templates.TemplateVersions;

public sealed class PublishTemplateVersionCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<PublishTemplateVersionCommand, Result<TemplateVersionSummary>>
{
    public async ValueTask<Result<TemplateVersionSummary>> Handle(
        PublishTemplateVersionCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsTemplateAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure<TemplateVersionSummary>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage templates."));
        }

        var version = await db.TemplateVersions.FirstOrDefaultAsync(
            v => v.Id == request.VersionId && v.ReportTypeId == request.ReportTypeId, cancellationToken);

        if (version is null)
        {
            return Result.Failure<TemplateVersionSummary>(Error.NotFound("TEMPLATE_VERSION_NOT_FOUND", "Template version not found."));
        }

        if (version.Status != TemplateVersionStatus.Draft)
        {
            return Result.Failure<TemplateVersionSummary>(
                Error.Conflict("NOT_A_DRAFT", "Only a draft version can be published."));
        }

        var currentlyPublished = await db.TemplateVersions.FirstOrDefaultAsync(
            v => v.ReportTypeId == request.ReportTypeId && v.Status == TemplateVersionStatus.Published, cancellationToken);

        var now = clock.UtcNow;
        version.Publish(now);
        currentlyPublished?.Retire(now);

        await db.SaveChangesAsync(cancellationToken);

        return TemplateVersionSummary.FromEntity(version);
    }
}
