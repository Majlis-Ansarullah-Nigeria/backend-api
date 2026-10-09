using HRS.Domain.Common;
using HRS.Domain.Submissions;
using Mediator;

namespace HRS.Application.Features.Submissions.ReportInstances;

/// <summary>Autosave/manual save (FR-SUB-003). Id/UnitId resolve the same two ways as the GET
/// query. IfMatch is required once an instance already exists — the client's last-known
/// RowVersion, echoed back from either the GET or a prior Save — and is ignored on the very first
/// save, since there's nothing yet to match against. Answers is a plain dictionary, not a
/// JsonElement — the latter has no OpenAPI schema (every template's fields differ) and generates
/// an empty TS interface on the frontend; a dictionary at least declares "field key to arbitrary
/// value," which orval turns into a real Record type.</summary>
public sealed record SaveReportDraftCommand(
    Guid UserId, Guid Id, Guid UnitId, Dictionary<string, object?> Answers, string? IfMatch) : IRequest<Result<SaveReportDraftResult>>;

public sealed record SaveReportDraftResult(Guid Id, ReportInstanceStatus Status, string RowVersion);
