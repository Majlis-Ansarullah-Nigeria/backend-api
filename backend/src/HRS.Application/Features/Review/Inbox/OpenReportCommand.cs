using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Review.Inbox;

/// <summary>FR-REV-002's "mark as read" — structurally just Submitted -&gt; InReview
/// (ReportInstance.Open's own doc comment: "this is what 'unread' structurally means; there's no
/// separate IsRead flag"). Idempotent: called automatically whenever the Secretariat opens a
/// report's detail view, so any status other than Submitted (already opened, Returned,
/// Acknowledged) is a harmless no-op rather than a conflict — opening it again isn't an error.
/// Doesn't notify anyone: FR-NOT-002's event list has no "marked read" event.</summary>
public sealed record OpenReportCommand(Guid UserId, Guid ReportId) : IRequest<Result>;

public sealed class OpenReportCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<OpenReportCommand, Result>
{
    public async ValueTask<Result> Handle(OpenReportCommand request, CancellationToken cancellationToken)
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

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Auto-fired on every view, often from more than one near-simultaneous request
                // (two tabs, a double-render) — whoever else raced this to InReview first already
                // achieved the only thing this command cares about, so there's nothing to retry
                // or report back as a conflict.
            }
        }

        return Result.Success();
    }
}
