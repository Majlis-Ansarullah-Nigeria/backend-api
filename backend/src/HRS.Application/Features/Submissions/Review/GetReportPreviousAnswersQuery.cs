using Mediator;
using HRS.Domain.Common;

namespace HRS.Application.Features.Submissions.Review;

/// <summary>FR-SUB-008 — the unit's most recent PRIOR submission of the same report type.
/// Returns null (not an error) when none exists, so the frontend can simply hide the "Copy from
/// last period" button. Which fields are actually worth copying ("text and settings", not raw
/// figures) is a frontend decision — this just hands back everything that was answered.</summary>
public sealed record GetReportPreviousAnswersQuery(Guid UserId, Guid ReportId, Guid UnitId) : IRequest<Result<PreviousReportAnswers?>>;

public sealed record PreviousReportAnswers(string PeriodLabel, Dictionary<string, object?> Answers);
