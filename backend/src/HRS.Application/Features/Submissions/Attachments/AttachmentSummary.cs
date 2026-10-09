using HRS.Domain.Submissions;

namespace HRS.Application.Features.Submissions.Attachments;

/// <summary>What the report form needs to show an already-uploaded (or just-uploaded) file —
/// returned by the complete-upload call and re-injected into every FileUpload field's answer by
/// GetReportInstanceForEditingQuery, since attachments are never part of the autosaved answers
/// JSON (see ReportFormScreen.tsx's stripFileFields).</summary>
public sealed record AttachmentSummary(Guid Id, string FieldKey, string FileName, string ContentType, long SizeBytes)
{
    public static AttachmentSummary From(ReportAttachment attachment) =>
        new(attachment.Id, attachment.FieldKey, attachment.FileName, attachment.ContentType, attachment.SizeBytes);
}
