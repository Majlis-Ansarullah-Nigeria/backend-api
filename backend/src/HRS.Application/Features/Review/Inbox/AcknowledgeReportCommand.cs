using HRS.Application.Common.Interfaces;
using HRS.Application.Common.Options;
using HRS.Application.Features.Notifications;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Notifications;
using HRS.Domain.Submissions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HRS.Application.Features.Review.Inbox;

/// <summary>FR-REV-002's "acknowledge" — the lifecycle's final state. Auto-opens first if the
/// report is still Submitted (ReportInstance.Open then Acknowledge in one call), so the Secretariat
/// doesn't have to click "mark as read" before acting on a report they haven't viewed yet.</summary>
public sealed record AcknowledgeReportCommand(Guid UserId, Guid ReportId) : IRequest<Result>;

public sealed class AcknowledgeReportCommandHandler(
    IAppDbContext db, IAccessPolicy accessPolicy, IClock clock, IOptions<AppOptions> appOptions)
    : IRequestHandler<AcknowledgeReportCommand, Result>
{
    public async ValueTask<Result> Handle(AcknowledgeReportCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "Only the Secretariat has an inbox."));
        }

        var instance = await db.ReportInstances.FirstOrDefaultAsync(i => i.Id == request.ReportId, cancellationToken);
        if (instance is null)
        {
            return Result.Failure(Error.NotFound("REPORT_NOT_FOUND", "That report doesn't exist."));
        }

        if (instance.Status == ReportInstanceStatus.Submitted)
        {
            instance.Open();
        }

        if (instance.Status != ReportInstanceStatus.InReview)
        {
            return Result.Failure(Error.Conflict("CANNOT_ACKNOWLEDGE", "This report can't be acknowledged in its current status."));
        }

        var window = await db.SubmissionWindows.AsNoTracking().FirstAsync(w => w.Id == instance.WindowId, cancellationToken);
        var reportType = await db.ReportTypes.AsNoTracking().FirstAsync(r => r.Id == window.ReportTypeId, cancellationToken);
        var unitName = await db.Units.AsNoTracking().Where(u => u.Id == instance.UnitId).Select(u => u.Name).FirstAsync(cancellationToken);

        var now = clock.UtcNow;
        instance.Acknowledge(now);

        var placeholders = new Dictionary<string, string>
        {
            ["unitName"] = unitName,
            ["reportTypeName"] = reportType.Name,
            ["periodLabel"] = window.PeriodLabel,
            ["link"] = $"{appOptions.Value.PublicUrl.TrimEnd('/')}/reports/{instance.Id}/edit?unit={instance.UnitId}",
        };
        await OutboxWriter.EnqueueForUnitHeadsAsync(db, instance.UnitId, NotificationEventType.ReportAcknowledged, placeholders, now, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
