using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed record CreateSubmissionWindowCommand(
    Guid UserId,
    Guid ReportTypeId,
    string PeriodLabel,
    DateTimeOffset OpensAt,
    int GraceDays,
    DateTimeOffset ClosesAt,
    IReadOnlyList<WindowDueDate> DueDates,
    IReadOnlyList<Guid>? TargetUnitIds = null) : IRequest<Result<CreateSubmissionWindowResult>>;

public sealed record CreateSubmissionWindowResult(SubmissionWindowSummary Window, IReadOnlyList<string> Warnings);
