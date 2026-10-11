using System.Text.Json;
using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.Review;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Review.Inbox;

/// <summary>FR-REV-001's read-only detail pane — no action buttons here (mark-read, acknowledge,
/// return, flag are M7.2; comments/assignment are M7.3). Unlike the observer's child-report view
/// (M6.2), confidential fields are NOT masked: that hiding rule is for observers, not the
/// recipient who is meant to review everything (hrs-domain: confidentiality hides fields "from
/// observers").</summary>
public sealed record GetInboxReportDetailQuery(Guid UserId, Guid ReportId) : IRequest<Result<InboxReportDetail>>;

public sealed record InboxReportDetail(ReportInstanceStatus Status, bool IsLate, DateTimeOffset? SubmittedAt, ReportReceiptData Receipt);

public sealed class GetInboxReportDetailQueryHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<GetInboxReportDetailQuery, Result<InboxReportDetail>>
{
    public async ValueTask<Result<InboxReportDetail>> Handle(GetInboxReportDetailQuery request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure<InboxReportDetail>(Error.Forbidden("FORBIDDEN", "Only the Secretariat has an inbox."));
        }

        var instance = await db.ReportInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == request.ReportId, cancellationToken);
        if (instance is null || instance.Status is ReportInstanceStatus.NotStarted or ReportInstanceStatus.Draft or ReportInstanceStatus.Missed)
        {
            return Result.Failure<InboxReportDetail>(Error.NotFound("REPORT_NOT_FOUND", "That report doesn't exist."));
        }

        var window = await db.SubmissionWindows.AsNoTracking().FirstAsync(w => w.Id == instance.WindowId, cancellationToken);
        var reportType = await db.ReportTypes.AsNoTracking().FirstAsync(r => r.Id == window.ReportTypeId, cancellationToken);
        var unitRow = await db.Units.AsNoTracking()
            .Where(u => u.Id == instance.UnitId)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { u.Name, LevelCode = l.Code })
            .FirstAsync(cancellationToken);
        var templateVersion = await db.TemplateVersions.AsNoTracking().FirstAsync(v => v.Id == instance.TemplateVersionId, cancellationToken);
        var schema = JsonSerializer.Deserialize<TemplateSchema>(templateVersion.SchemaJson, JsonSerializerOptions.Web)!;

        var receipt = await ReportReceiptBuilder.BuildAsync(
            db, instance, schema, reportType.Name, window.PeriodLabel, unitRow.Name, (int)unitRow.LevelCode,
            instance.ReferenceNumber, instance.SubmittedAt ?? clock.UtcNow, cancellationToken, hideConfidentialValues: false);

        return Result.Success(new InboxReportDetail(instance.Status, instance.IsLate, instance.SubmittedAt, receipt));
    }
}
