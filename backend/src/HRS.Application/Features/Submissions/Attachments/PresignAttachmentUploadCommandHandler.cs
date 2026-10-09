using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Submissions.ReportInstances;
using HRS.Domain.Common;
using HRS.Domain.Submissions;
using HRS.Domain.Templates;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Submissions.Attachments;

public sealed class PresignAttachmentUploadCommandHandler(
    IAppDbContext db, IAccessPolicy accessPolicy, IFileStorage fileStorage, IClock clock)
    : IRequestHandler<PresignAttachmentUploadCommand, Result<PresignedAttachmentUpload>>
{
    public async ValueTask<Result<PresignedAttachmentUpload>> Handle(PresignAttachmentUploadCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.CanEditReportForUnitAsync(request.UserId, request.UnitId, cancellationToken))
        {
            return Result.Failure<PresignedAttachmentUpload>(Error.Forbidden("FORBIDDEN", "You don't have access to this unit's reports."));
        }

        if (!AttachmentPolicy.AllowedContentTypes.Contains(request.ContentType))
        {
            return Result.Failure<PresignedAttachmentUpload>(
                Error.Validation("ATTACHMENT_TYPE_NOT_ALLOWED", "This file type isn't allowed. Attach images, PDFs or Office documents."));
        }

        if (request.SizeBytes <= 0 || request.SizeBytes > AttachmentPolicy.MaxFileSizeBytes)
        {
            return Result.Failure<PresignedAttachmentUpload>(Error.Validation("ATTACHMENT_TOO_LARGE", "Files must be 20 MB or smaller."));
        }

        // Same dual-mode resolution as Get/SaveReportDraft — id is either a real instance id or,
        // when nothing's been saved yet, the window id. An instance may since have been created
        // for that (window, unit) pair without the frontend learning its real id, so that's
        // checked too before concluding none exists (falling through to "create a new one" would
        // violate the one-instance-per-(window,unit) unique index).
        var instance = await db.ReportInstances.FirstOrDefaultAsync(i => i.Id == request.ReportId && i.UnitId == request.UnitId, cancellationToken);
        instance ??= await db.ReportInstances.FirstOrDefaultAsync(i => i.WindowId == request.ReportId && i.UnitId == request.UnitId, cancellationToken);

        var window = await db.SubmissionWindows.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == (instance != null ? instance.WindowId : request.ReportId), cancellationToken);
        if (window is null)
        {
            return Result.Failure<PresignedAttachmentUpload>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        var reportType = await db.ReportTypes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == window.ReportTypeId, cancellationToken);
        if (reportType is null)
        {
            return Result.Failure<PresignedAttachmentUpload>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        if (!reportType.AllowAttachments)
        {
            return Result.Failure<PresignedAttachmentUpload>(
                Error.Validation("ATTACHMENTS_NOT_ALLOWED", "This report type doesn't accept attachments."));
        }

        var unitRow = await db.Units.AsNoTracking()
            .Where(u => u.Id == request.UnitId)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { l.Code })
            .FirstOrDefaultAsync(cancellationToken);
        if (unitRow is null)
        {
            return Result.Failure<PresignedAttachmentUpload>(Error.NotFound("REPORT_NOT_FOUND", "Report not found."));
        }

        var now = clock.UtcNow;
        var editError = ReportEditability.Check(window, instance, unitRow.Code, request.UnitId, now);
        if (editError is not null)
        {
            return Result.Failure<PresignedAttachmentUpload>(editError);
        }

        // First attachment on a never-saved report creates the instance, same as the first Save —
        // a user may well attach a photo before typing anything else.
        if (instance is null)
        {
            var published = await db.TemplateVersions.AsNoTracking()
                .FirstOrDefaultAsync(v => v.ReportTypeId == reportType.Id && v.Status == TemplateVersionStatus.Published, cancellationToken);
            if (published is null)
            {
                return Result.Failure<PresignedAttachmentUpload>(
                    Error.Conflict("NO_PUBLISHED_TEMPLATE", "This report type has no published template yet."));
            }

            instance = ReportInstance.Create(Guid.CreateVersion7(), window.Id, request.UnitId, published.Id, now);
            db.ReportInstances.Add(instance);
        }

        var currentTotal = await db.ReportAttachments
            .Where(a => a.ReportInstanceId == instance.Id)
            .SumAsync(a => (long?)a.SizeBytes, cancellationToken) ?? 0;

        if (currentTotal + request.SizeBytes > AttachmentPolicy.MaxReportTotalBytes)
        {
            return Result.Failure<PresignedAttachmentUpload>(
                Error.Conflict("ATTACHMENT_BUDGET_EXCEEDED", "This report's attachments would exceed the 100 MB limit."));
        }

        var attachmentId = Guid.CreateVersion7();
        var storageKey = $"reports/{instance.Id:N}/{attachmentId:N}/{SanitizeFileName(request.FileName)}";

        var attachment = ReportAttachment.Create(
            attachmentId, instance.Id, request.FieldKey, request.FileName, request.ContentType, request.SizeBytes, storageKey, request.UserId, now);
        db.ReportAttachments.Add(attachment);

        await db.SaveChangesAsync(cancellationToken);

        var presigned = fileStorage.CreatePresignedUpload(storageKey, TimeSpan.FromMinutes(15));
        return new PresignedAttachmentUpload(attachment.Id, presigned.Url);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(fileName.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return cleaned.Length == 0 ? "file" : cleaned;
    }
}
