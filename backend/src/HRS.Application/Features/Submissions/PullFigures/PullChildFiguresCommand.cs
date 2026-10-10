using Mediator;
using HRS.Domain.Common;

namespace HRS.Application.Features.Submissions.PullFigures;

/// <summary>FR-SUB-007: aggregates numeric fields from the included children's submitted
/// reports into this draft. IncludedUnitIds narrows which submitted children contribute (the head
/// deselecting an outlier); omitted, every submitted child is included. Safe to call repeatedly
/// ("Pull again") — a field the head has since edited away from its last-pulled value is left
/// alone, not silently overwritten.</summary>
public sealed record PullChildFiguresCommand(
    Guid UserId,
    Guid ReportId,
    Guid UnitId,
    IReadOnlyList<Guid>? IncludedUnitIds,
    string? OverrideNote,
    string? IfMatch) : IRequest<Result<PullChildFiguresResult>>;

public sealed record PullChildFiguresResult(Guid Id, string RowVersion, PullSummary Summary);
