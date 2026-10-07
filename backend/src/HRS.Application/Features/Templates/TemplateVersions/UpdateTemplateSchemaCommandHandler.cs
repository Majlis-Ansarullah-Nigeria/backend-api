using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Templates.TemplateVersions;

public sealed class UpdateTemplateSchemaCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<UpdateTemplateSchemaCommand, Result<TemplateVersionSummary>>
{
    public async ValueTask<Result<TemplateVersionSummary>> Handle(
        UpdateTemplateSchemaCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsTemplateAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure<TemplateVersionSummary>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat can manage templates."));
        }

        var reportType = await db.ReportTypes.FirstOrDefaultAsync(r => r.Id == request.ReportTypeId, cancellationToken);

        if (reportType is null)
        {
            return Result.Failure<TemplateVersionSummary>(Error.NotFound("REPORT_TYPE_NOT_FOUND", "Report type not found."));
        }

        if (reportType.IsArchived)
        {
            return Result.Failure<TemplateVersionSummary>(
                Error.Conflict("REPORT_TYPE_ARCHIVED", "Can't edit a template for an archived report type."));
        }

        var versions = await db.TemplateVersions
            .Where(v => v.ReportTypeId == request.ReportTypeId)
            .ToListAsync(cancellationToken);

        var schemaJson = JsonSerializer.Serialize(request.Schema, JsonSerializerOptions.Web);
        var draft = versions.FirstOrDefault(v => v.Status == TemplateVersionStatus.Draft);

        TemplateVersion version;
        if (draft is not null)
        {
            draft.UpdateDraftSchema(schemaJson);
            version = draft;
        }
        else
        {
            var nextVersionNumber = versions.Count == 0 ? 1 : versions.Max(v => v.VersionNumber) + 1;
            version = TemplateVersion.CreateDraft(
                Guid.CreateVersion7(), request.ReportTypeId, nextVersionNumber, schemaJson, clock.UtcNow);
            db.TemplateVersions.Add(version);
        }

        await db.SaveChangesAsync(cancellationToken);

        return TemplateVersionSummary.FromEntity(version);
    }
}
