using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Options;
using HRS.Application.Features.Notifications;
using HRS.Domain.Common;
using HRS.Domain.Notifications;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRS.Application.Features.Templates.TemplateVersions;

public sealed class PublishTemplateVersionCommandHandler(
    IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IOptions<AppOptions> appOptions)
    : IRequestHandler<PublishTemplateVersionCommand, Result<TemplateVersionSummary>>
{
    private static readonly HashSet<ReportInstanceStatus> StaleDraftStatuses = [ReportInstanceStatus.Draft, ReportInstanceStatus.Returned];

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

        // FR-NOT-002's "template changed" — scoped to units actually mid-filling against the
        // version that just got superseded, not every head of every unit this report type
        // applies to (nothing else in this codebase notifies an entire organization for an
        // internal template edit, and a NotStarted report has no row yet to even be "stuck" on
        // the old version).
        if (currentlyPublished is not null)
        {
            var reportType = await db.ReportTypes.AsNoTracking().FirstAsync(r => r.Id == request.ReportTypeId, cancellationToken);
            var affectedInstances = await db.ReportInstances.AsNoTracking()
                .Where(i => i.TemplateVersionId == currentlyPublished.Id && StaleDraftStatuses.Contains(i.Status))
                .Select(i => new { i.Id, i.UnitId })
                .ToListAsync(cancellationToken);

            if (affectedInstances.Count > 0)
            {
                var unitIds = affectedInstances.Select(i => i.UnitId).Distinct().ToList();
                var unitNamesById = await db.Units.AsNoTracking()
                    .Where(u => unitIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);
                var publicUrl = appOptions.Value.PublicUrl.TrimEnd('/');

                foreach (var affected in affectedInstances)
                {
                    var placeholders = new Dictionary<string, string>
                    {
                        ["unitName"] = unitNamesById.GetValueOrDefault(affected.UnitId, "Your unit"),
                        ["reportTypeName"] = reportType.Name,
                        ["link"] = $"{publicUrl}/reports/{affected.Id}/edit?unit={affected.UnitId}",
                    };
                    await OutboxWriter.EnqueueForUnitHeadsAsync(
                        db, affected.UnitId, NotificationEventType.TemplateChanged, placeholders, now, cancellationToken);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return TemplateVersionSummary.FromEntity(version);
    }
}
