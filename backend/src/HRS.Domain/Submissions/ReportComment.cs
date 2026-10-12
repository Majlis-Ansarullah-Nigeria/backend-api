namespace HRS.Domain.Submissions;

/// <summary>
/// FR-REV-003's "inline comments anchored to a field; threaded replies with the submitter" —
/// Secretariat <-> current head/delegate only (FR-AC-005: observers never see review comments; the
/// separate, unit-scoped PrivateNote from M6.4 already covers the observer-private case, so this
/// entity carries no "visibility" flag the way SRS §8's data dictionary sketches). Two levels only:
/// a root comment (ParentId null, FieldKey required) opens a thread on one field; a reply
/// (ParentId = that root's id) inherits the root's FieldKey and can't itself be replied to — a flat
/// list under each field, not arbitrary nesting. Append-only, like ReportRevision's immutable
/// history — no edit or delete.
/// </summary>
public sealed class ReportComment
{
    private ReportComment()
    {
    }

    private ReportComment(Guid id, Guid reportInstanceId, string fieldKey, Guid? parentId, Guid authorUserId, string body, DateTimeOffset now)
    {
        Id = id;
        ReportInstanceId = reportInstanceId;
        FieldKey = fieldKey;
        ParentId = parentId;
        AuthorUserId = authorUserId;
        Body = body;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid ReportInstanceId { get; private set; }

    public string FieldKey { get; private set; } = string.Empty;

    /// <summary>Null for a thread's root comment; the root's own Id for every reply underneath it.</summary>
    public Guid? ParentId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static ReportComment Create(Guid id, Guid reportInstanceId, string fieldKey, Guid? parentId, Guid authorUserId, string body, DateTimeOffset now) =>
        new(id, reportInstanceId, fieldKey, parentId, authorUserId, body, now);
}
