using Mediator;
using HRS.Domain.Common;

namespace HRS.Application.Features.Submissions.Review;

/// <summary>Backs both "Download draft PDF" (pre-submit — no ReferenceNumber yet) and "Download
/// PDF" (post-submit) on the review/submitted screens. Deliberately has no ReportEditability
/// gate — a user can always re-download their own receipt, Acknowledged reports included.</summary>
public sealed record GetReportReceiptPdfQuery(Guid UserId, Guid ReportId, Guid UnitId) : IRequest<Result<byte[]>>;
