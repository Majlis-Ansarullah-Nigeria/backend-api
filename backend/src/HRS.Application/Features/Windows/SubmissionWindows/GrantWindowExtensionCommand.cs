using HRS.Domain.Common;
using Mediator;

namespace HRS.Application.Features.Windows.SubmissionWindows;

public sealed record GrantWindowExtensionCommand(
    Guid UserId,
    Guid ReportTypeId,
    Guid WindowId,
    IReadOnlyList<Guid> UnitIds,
    DateTimeOffset NewDueAt,
    DateTimeOffset NewClosesAt,
    string Reason) : IRequest<Result<SubmissionWindowSummary>>;
