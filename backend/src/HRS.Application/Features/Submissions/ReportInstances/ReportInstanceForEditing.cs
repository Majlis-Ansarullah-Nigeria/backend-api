using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Submissions;

namespace HRS.Application.Features.Submissions.ReportInstances;

/// <summary>
/// Everything the report-form screen needs in one call: Id is null when nothing has been saved
/// yet (a "virtual" NotStarted report — the first Save is what actually creates the row).
/// RowVersion is the If-Match value to echo back on the next Save; null for the same reason Id is.
/// Answers is a plain dictionary, not a JsonElement — see SaveReportDraftCommand's own note.
/// </summary>
public sealed record ReportInstanceForEditing(
    Guid? Id,
    Guid WindowId,
    Guid UnitId,
    string UnitName,
    Guid ReportTypeId,
    string ReportTypeName,
    string PeriodLabel,
    Guid TemplateVersionId,
    int TemplateVersionNumber,
    TemplateSchema Schema,
    int UnitLevel,
    Dictionary<string, object?> Answers,
    ReportInstanceStatus Status,
    bool CanSave,
    DateTimeOffset OpensAt,
    DateTimeOffset? DueAt,
    DateTimeOffset ClosesAt,
    DateTimeOffset? RevisionDeadline,
    string? RowVersion);
