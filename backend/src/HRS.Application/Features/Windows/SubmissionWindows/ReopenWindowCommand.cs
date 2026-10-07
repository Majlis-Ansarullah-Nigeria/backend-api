using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed record ReopenWindowCommand(
    Guid UserId,
    Guid ReportTypeId,
    Guid WindowId,
    IReadOnlyList<Guid> UnitIds,
    DateTimeOffset ReopenedUntil,
    string Reason) : IRequest<Result<SubmissionWindowSummary>>;
