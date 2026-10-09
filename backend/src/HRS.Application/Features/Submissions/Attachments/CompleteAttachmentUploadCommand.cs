using Mediator;
using HRS.Domain.Common;

namespace HRS.Application.Features.Submissions.Attachments;

public sealed record CompleteAttachmentUploadCommand(Guid UserId, Guid ReportId, Guid UnitId, Guid AttachmentId)
    : IRequest<Result<AttachmentSummary>>;
