using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.Attachments;

/// <summary>Runs once the browser has PUT the bytes directly to storage: re-measures the real
/// uploaded size (never trusts the client's presign-time declaration), scans it, and either
/// accepts it or removes it outright. Scanning runs synchronously here rather than via a
/// background job + polling endpoint — ClamAV scans of files this size (&lt;=20 MB) over the
/// local Docker network are fast enough that the extra moving parts aren't worth it.</summary>
public sealed class CompleteAttachmentUploadCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IFileStorage fileStorage, IVirusScanner virusScanner, IClock clock)
    : IRequestHandler<CompleteAttachmentUploadCommand, Result<AttachmentSummary>>
{
    public async ValueTask<Result<AttachmentSummary>> Handle(CompleteAttachmentUploadCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.CanEditReportForUnitAsync(request.UserId, request.UnitId, cancellationToken))
        {
            return Result.Failure<AttachmentSummary>(Error.Forbidden("FORBIDDEN", "You don't have access to this unit's reports."));
        }

        var instanceResult = await ExistingReportResolver.ResolveAsync(db, request.ReportId, request.UnitId, clock.UtcNow, cancellationToken);
        if (instanceResult.IsFailure)
        {
            return Result.Failure<AttachmentSummary>(instanceResult.Error);
        }

        var instance = instanceResult.Value;
        var attachment = await db.ReportAttachments
            .FirstOrDefaultAsync(a => a.Id == request.AttachmentId && a.ReportInstanceId == instance.Id, cancellationToken);
        if (attachment is null)
        {
            return Result.Failure<AttachmentSummary>(Error.NotFound("ATTACHMENT_NOT_FOUND", "Attachment not found."));
        }

        // A retried complete call (e.g. a flaky network) on an already-accepted attachment is a
        // no-op success, not an error.
        if (attachment.Status == ReportAttachmentStatus.Clean)
        {
            return AttachmentSummary.From(attachment);
        }

        await using var stored = await fileStorage.GetAsync(attachment.StorageKey, cancellationToken);
        if (stored is null)
        {
            return Result.Failure<AttachmentSummary>(
                Error.Validation("ATTACHMENT_UPLOAD_NOT_FOUND", "The file hasn't finished uploading yet."));
        }

        if (stored.SizeBytes > AttachmentPolicy.MaxFileSizeBytes)
        {
            await RejectAsync(attachment, cancellationToken);
            return Result.Failure<AttachmentSummary>(Error.Validation("ATTACHMENT_TOO_LARGE", "Files must be 20 MB or smaller."));
        }

        var otherAttachmentsTotal = await db.ReportAttachments
            .Where(a => a.ReportInstanceId == instance.Id && a.Id != attachment.Id)
            .SumAsync(a => (long?)a.SizeBytes, cancellationToken) ?? 0;
        if (otherAttachmentsTotal + stored.SizeBytes > AttachmentPolicy.MaxReportTotalBytes)
        {
            await RejectAsync(attachment, cancellationToken);
            return Result.Failure<AttachmentSummary>(
                Error.Conflict("ATTACHMENT_BUDGET_EXCEEDED", "This report's attachments would exceed the 100 MB limit."));
        }

        var scanResult = await virusScanner.ScanAsync(stored.Content, cancellationToken);
        if (scanResult == VirusScanResult.Infected)
        {
            await RejectAsync(attachment, cancellationToken);
            return Result.Failure<AttachmentSummary>(
                Error.Validation("ATTACHMENT_INFECTED", "This file failed a virus scan and was not accepted."));
        }

        attachment.MarkClean(stored.SizeBytes);
        await db.SaveChangesAsync(cancellationToken);

        return AttachmentSummary.From(attachment);
    }

    private async Task RejectAsync(ReportAttachment attachment, CancellationToken cancellationToken)
    {
        await fileStorage.DeleteAsync(attachment.StorageKey, cancellationToken);
        db.ReportAttachments.Remove(attachment);
        await db.SaveChangesAsync(cancellationToken);
    }
}
