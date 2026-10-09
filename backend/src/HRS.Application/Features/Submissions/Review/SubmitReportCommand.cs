using Mediator;
using HRS.Domain.Common;

namespace HRS.Application.Features.Submissions.Review;

public sealed record SubmitReportCommand(Guid UserId, Guid ReportId, Guid UnitId, bool Confirmed, string? IdempotencyKey)
    : IRequest<Result<SubmitReportResult>>;

public sealed record SubmitReportResult(Guid Id, string ReferenceNumber, DateTimeOffset SubmittedAt, bool IsLate);
