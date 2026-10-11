using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Review.Inbox;

/// <summary>FR-REV-002's "return for revision with a required comment". Auto-opens first if the
/// report is still Submitted, same reasoning as AcknowledgeReportCommand. The revision deadline
/// uses ReportInstance.Return's own +7-day default — no override in v1 (not asked for by
/// FR-REV-002), so the resubmit window (BR-WIN-06) stays consistent everywhere it's granted.</summary>
public sealed record ReturnReportCommand(Guid UserId, Guid ReportId, string Comment) : IRequest<Result>;

public sealed class ReturnReportCommandHandler(
    IAppDbContext db, IAccessPolicy accessPolicy, IIdentityService identityService, IEmailSender emailSender, IClock clock)
    : IRequestHandler<ReturnReportCommand, Result>
{
    public async ValueTask<Result> Handle(ReturnReportCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "Only the Secretariat has an inbox."));
        }

        if (string.IsNullOrWhiteSpace(request.Comment))
        {
            return Result.Failure(Error.Validation("COMMENT_REQUIRED", "Explain what needs to change before returning this report."));
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
            return Result.Failure(Error.Conflict("CANNOT_RETURN", "This report can't be returned in its current status."));
        }

        var window = await db.SubmissionWindows.AsNoTracking().FirstAsync(w => w.Id == instance.WindowId, cancellationToken);
        var reportType = await db.ReportTypes.AsNoTracking().FirstAsync(r => r.Id == window.ReportTypeId, cancellationToken);
        var unitName = await db.Units.AsNoTracking().Where(u => u.Id == instance.UnitId).Select(u => u.Name).FirstAsync(cancellationToken);

        var now = clock.UtcNow;
        var comment = request.Comment.Trim();
        instance.Return(comment, now);
        await db.SaveChangesAsync(cancellationToken);

        await ReportNotifier.NotifyUnitHeadsAsync(
            db, identityService, emailSender, instance.UnitId, now,
            $"{reportType.Name} — {window.PeriodLabel} returned for revision",
            $"The National Secretariat has returned {unitName}'s {window.PeriodLabel} {reportType.Name} for revision:\n\n{comment}",
            cancellationToken);

        return Result.Success();
    }
}
