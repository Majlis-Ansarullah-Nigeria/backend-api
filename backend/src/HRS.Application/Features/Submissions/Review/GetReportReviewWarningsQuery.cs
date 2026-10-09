using Mediator;
using HRS.Domain.Common;

namespace HRS.Application.Features.Submissions.Review;

public sealed record GetReportReviewWarningsQuery(Guid UserId, Guid ReportId, Guid UnitId) : IRequest<Result<ReviewWarningsResult>>;

public sealed record ReviewWarningsResult(IReadOnlyList<string> Warnings);
