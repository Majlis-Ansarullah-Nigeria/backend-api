using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.Attachments;

public sealed class DeleteAttachmentCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IFileStorage fileStorage, IClock clock)
    : IRequestHandler<DeleteAttachmentCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteAttachmentCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.CanEditReportForUnitAsync(request.UserId, request.UnitId, cancellationToken))
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "You don't have access to this unit's reports."));
        }

        var instanceResult = await ExistingReportResolver.ResolveAsync(db, request.ReportId, request.UnitId, clock.UtcNow, cancellationToken);
        if (instanceResult.IsFailure)
        {
            return Result.Failure(instanceResult.Error);
        }

        var attachment = await db.ReportAttachments
            .FirstOrDefaultAsync(a => a.Id == request.AttachmentId && a.ReportInstanceId == instanceResult.Value.Id, cancellationToken);
        if (attachment is null)
        {
            return Result.Failure(Error.NotFound("ATTACHMENT_NOT_FOUND", "Attachment not found."));
        }

        await fileStorage.DeleteAsync(attachment.StorageKey, cancellationToken);
        db.ReportAttachments.Remove(attachment);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
