using Mediator;
using HRS.Domain.Common;

namespace HRS.Application.Features.Submissions.Attachments;

public sealed record PresignAttachmentUploadCommand(
    Guid UserId, Guid ReportId, Guid UnitId, string FieldKey, string FileName, string ContentType, long SizeBytes)
    : IRequest<Result<PresignedAttachmentUpload>>;

public sealed record PresignedAttachmentUpload(Guid AttachmentId, string UploadUrl);
