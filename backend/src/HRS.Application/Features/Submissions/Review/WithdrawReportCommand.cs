using Mediator;
using HRS.Domain.Common;

namespace HRS.Application.Features.Submissions.Review;

public sealed record WithdrawReportCommand(Guid UserId, Guid ReportId, Guid UnitId) : IRequest<Result>;
