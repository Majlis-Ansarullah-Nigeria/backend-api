using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed record UpdateSubmissionWindowCommand(
    Guid UserId,
    Guid ReportTypeId,
    Guid WindowId,
    string PeriodLabel,
    DateTimeOffset OpensAt,
    int GraceDays,
    DateTimeOffset ClosesAt,
    IReadOnlyList<WindowDueDate> DueDates) : IRequest<Result<CreateSubmissionWindowResult>>;
