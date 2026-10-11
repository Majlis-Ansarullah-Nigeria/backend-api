using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Review.Inbox;

/// <summary>FR-REV-002's "flag for follow-up" — a reviewer's own marker, not part of the
/// lifecycle state machine (ReportInstance.SetFlagged's own doc comment: settable "in any
/// post-submission status"). No notification: FR-NOT-002's event list has no "flagged" event, and
/// flagging is Secretariat-internal bookkeeping, not something the submitter needs to know about.</summary>
public sealed record SetReportFlaggedCommand(Guid UserId, Guid ReportId, bool IsFlagged) : IRequest<Result>;

public sealed class SetReportFlaggedCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<SetReportFlaggedCommand, Result>
{
    public async ValueTask<Result> Handle(SetReportFlaggedCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.HasRoleAsync(request.UserId, RoleName.Secretariat, cancellationToken))
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "Only the Secretariat has an inbox."));
        }

        var instance = await db.ReportInstances.FirstOrDefaultAsync(i => i.Id == request.ReportId, cancellationToken);
        if (instance is null || instance.Status is ReportInstanceStatus.NotStarted or ReportInstanceStatus.Draft or ReportInstanceStatus.Missed)
        {
            return Result.Failure(Error.NotFound("REPORT_NOT_FOUND", "That report doesn't exist."));
        }

        instance.SetFlagged(request.IsFlagged);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
