namespace HRS.Domain.Submissions;

/// <summary>
/// One unit's filled report for one submission window (SRS §5 — the report lifecycle). Always
/// belongs to a Unit, never a User (hrs-domain: "Reports belong to a Unit, never to a User. Heads
/// change; history stays with the unit."). TemplateVersionId is pinned at creation so the form
/// always renders — and always answers questions about what was asked — against the exact
/// version it was filled against, even after a newer version publishes (FR-TPL-007).
///
/// This type only enforces the state machine itself. Window-open checks, the late-flag
/// computation, and WINDOW_CLOSED rejection are Application-handler concerns (M5.3/M5.5/M5.6) —
/// the same split SubmissionWindow already draws between domain-level date math and
/// handler-level orchestration. AnswersJson is an opaque blob here for the same reason
/// TemplateVersion.SchemaJson is: the structured shape and its validation belong to Application
/// (M5.3), not Domain.
/// </summary>
public sealed class ReportInstance
{
    private ReportInstance()
    {
    }

    private ReportInstance(Guid id, Guid windowId, Guid unitId, Guid templateVersionId, DateTimeOffset now)
    {
        Id = id;
        WindowId = windowId;
        UnitId = unitId;
        TemplateVersionId = templateVersionId;
        Status = ReportInstanceStatus.NotStarted;
        AnswersJson = "{}";
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid WindowId { get; private set; }

    public Guid UnitId { get; private set; }

    public Guid TemplateVersionId { get; private set; }

    public ReportInstanceStatus Status { get; private set; }

    public string AnswersJson { get; private set; } = "{}";

    public bool IsLate { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    public DateTimeOffset? ReturnedAt { get; private set; }

    public string? ReturnComment { get; private set; }

    public DateTimeOffset? RevisionDeadline { get; private set; }

    public DateTimeOffset? ReopenedAt { get; private set; }

    /// <summary>Postgres' native xmin system column, mapped as an EF Core concurrency token
    /// (Infrastructure) — the If-Match value autosave round-trips (backend/CLAUDE.md). Not set by
    /// application code; the database assigns and updates it.</summary>
    public uint RowVersion { get; private set; }

    /// <summary>Assigned once, on the FIRST submit — unique per report instance (hrs-domain
    /// skill), so a Returned-then-resubmitted report keeps the same reference it was first given
    /// rather than minting a new one each time it re-enters Submitted.</summary>
    public string? ReferenceNumber { get; private set; }

    /// <summary>FR-SUB-012's "No activity this period" — set on Submit, never cleared (a
    /// Returned-then-resubmitted nil report stays nil unless the resubmission itself declares
    /// otherwise).</summary>
    public bool IsNil { get; private set; }

    /// <summary>Who ran Submit (FR-OBS-002's "submitted ... by {name}" on the observer's read-only
    /// view). Null for reports submitted before this field existed. Re-set on every resubmit after
    /// a Return, so it always reflects the most recent submitter, unlike ReferenceNumber.</summary>
    public Guid? SubmittedByUserId { get; private set; }

    /// <summary>FR-SUB-007's "pull from children" result, opaque here for the same reason
    /// AnswersJson is — structure and merge logic belong to Application. Null until the first
    /// pull; overwritten (not appended) by each later pull, same way AnswersJson works for Save.</summary>
    public string? PullSummaryJson { get; private set; }

    /// <summary>FR-REV-002's "flag for follow-up" — a Secretariat reviewer's own marker, not part
    /// of the lifecycle state machine (can be set or cleared in any post-submission status). The
    /// action that flips this is M7.2's; this milestone only needs the column to filter by.</summary>
    public bool IsFlagged { get; private set; }

    /// <summary>FR-REV-004's "assign reports to Secretariat staff members" — current assignee
    /// only, not a history (reassigning just overwrites it, same as IsFlagged). Null means
    /// unassigned. No FK: a user reference here follows SubmittedByUserId's own precedent of not
    /// tying report rows to Identity's deletion/deactivation.</summary>
    public Guid? AssignedToUserId { get; private set; }

    /// <summary>How many ReportRevision entries this report has (FR-HIS-006) — the source of the
    /// next RevisionNo, and of whether a submit is a first submission or a resubmission.</summary>
    public int RevisionCount { get; private set; }

    public static ReportInstance Create(Guid id, Guid windowId, Guid unitId, Guid templateVersionId, DateTimeOffset now) =>
        new(id, windowId, unitId, templateVersionId, now);

    /// <summary>Saves a draft (manual save or autosave, FR-SUB-003). NotStarted -&gt; Draft on the
    /// first save; a Draft or Returned report just updates its answers in place — saving never
    /// moves a Returned report back to Draft by itself, only Submit (resubmission) does.</summary>
    public void Save(string answersJson)
    {
        if (Status is not (ReportInstanceStatus.NotStarted or ReportInstanceStatus.Draft or ReportInstanceStatus.Returned))
        {
            throw new InvalidOperationException($"Can't save a report in {Status} status — it's locked.");
        }

        AnswersJson = answersJson;

        if (Status == ReportInstanceStatus.NotStarted)
        {
            Status = ReportInstanceStatus.Draft;
        }
    }

    /// <summary>FR-SUB-007: merges pulled figures into the draft's answers and records the pull
    /// summary (for display, and so a later re-pull can tell a pulled value from a head's manual
    /// override). Same status guard as Save — pulling is just another way to fill the draft.</summary>
    public void RecordPull(string answersJson, string pullSummaryJson)
    {
        Save(answersJson);
        PullSummaryJson = pullSummaryJson;
    }

    /// <summary>Submits a Draft, or resubmits a Returned report (FR-SUB-009, BR-WIN-06). IsLate is
    /// computed by the caller against the window's (possibly extended) due date — see this type's
    /// own doc comment for why that math lives in the handler, not here. referenceNumber is also
    /// generated by the caller (FR-SUB-010 — the format depends on ReportType/SubmissionWindow
    /// data this type doesn't have); only assigned the first time, preserved across a
    /// Returned-then-resubmit cycle. isNil is FR-SUB-012's "No activity this period" — the caller
    /// is responsible for skipping the usual required-field check when it's set.</summary>
    public void Submit(bool isLate, string referenceNumber, bool isNil, DateTimeOffset now, Guid? submittedByUserId = null)
    {
        if (Status is not (ReportInstanceStatus.Draft or ReportInstanceStatus.Returned))
        {
            throw new InvalidOperationException($"Can't submit a report in {Status} status.");
        }

        Status = ReportInstanceStatus.Submitted;
        SubmittedAt = now;
        IsLate = isLate;
        ReturnComment = null;
        RevisionDeadline = null;
        ReferenceNumber ??= referenceNumber;
        IsNil = isNil;
        SubmittedByUserId = submittedByUserId;
    }

    /// <summary>The Secretariat opens a submitted report — this is what "unread" structurally
    /// means; there's no separate IsRead flag.</summary>
    public void Open()
    {
        if (Status != ReportInstanceStatus.Submitted)
        {
            throw new InvalidOperationException($"Can't open a report in {Status} status.");
        }

        Status = ReportInstanceStatus.InReview;
    }

    /// <summary>The head withdraws a submission while it's still unread (FR-SUB-009). The other
    /// half of that rule — the window must still be open — is a handler concern.</summary>
    public void Withdraw()
    {
        if (Status != ReportInstanceStatus.Submitted)
        {
            throw new InvalidOperationException($"Can't withdraw a report in {Status} status.");
        }

        Status = ReportInstanceStatus.Draft;
    }

    public void Acknowledge(DateTimeOffset now)
    {
        if (Status != ReportInstanceStatus.InReview)
        {
            throw new InvalidOperationException($"Can't acknowledge a report in {Status} status.");
        }

        Status = ReportInstanceStatus.Acknowledged;
        AcknowledgedAt = now;
    }

    /// <summary>Sends a report back with a comment. revisionDeadline defaults to 7 days out
    /// (BR-WIN-06's default), overridable by the reviewer.</summary>
    public void Return(string comment, DateTimeOffset now, DateTimeOffset? revisionDeadline = null)
    {
        if (Status != ReportInstanceStatus.InReview)
        {
            throw new InvalidOperationException($"Can't return a report in {Status} status.");
        }

        Status = ReportInstanceStatus.Returned;
        ReturnComment = comment;
        ReturnedAt = now;
        RevisionDeadline = revisionDeadline ?? now.AddDays(7);
    }

    /// <summary>The window job (M4.4/M8) marks an unsubmitted instance Missed once the hard close
    /// passes (BR-WIN-04). The draft's answers are kept for reference, not cleared.</summary>
    public void MarkMissed()
    {
        if (Status is not (ReportInstanceStatus.NotStarted or ReportInstanceStatus.Draft))
        {
            throw new InvalidOperationException($"Can't mark a report in {Status} status as missed.");
        }

        Status = ReportInstanceStatus.Missed;
    }

    /// <summary>Secretariat-only, rare, audited override (SRS §5.1) back into an editable state —
    /// either a Missed window being reopened for late submission (FR-WIN-006) or an Acknowledged
    /// report being reopened for correction.</summary>
    public void Reopen(DateTimeOffset now)
    {
        if (Status is not (ReportInstanceStatus.Missed or ReportInstanceStatus.Acknowledged))
        {
            throw new InvalidOperationException($"Can't reopen a report in {Status} status.");
        }

        Status = ReportInstanceStatus.Draft;
        ReopenedAt = now;
    }

    /// <summary>"Overdue" is a derived label (SRS §5.1), never stored: NotStarted or Draft, past
    /// its due date.</summary>
    public bool IsOverdue(DateTimeOffset now, DateTimeOffset dueAt) =>
        (Status is ReportInstanceStatus.NotStarted or ReportInstanceStatus.Draft) && now > dueAt;

    /// <summary>FR-HIS-006: snapshots the answers just submitted. Call right after
    /// <see cref="Submit"/>; the caller adds the returned revision to the same unit of work.</summary>
    public ReportRevision RecordSubmission(Guid? actorUserId, DateTimeOffset now)
    {
        if (Status != ReportInstanceStatus.Submitted)
        {
            throw new InvalidOperationException($"Can't record a submission for a report in {Status} status.");
        }

        var action = RevisionCount == 0 ? ReportRevisionAction.Submitted : ReportRevisionAction.Resubmitted;
        return NextRevision(action, actorUserId, now);
    }

    /// <summary>FR-HIS-006: records a withdrawal. Call right after <see cref="Withdraw"/>.</summary>
    public ReportRevision RecordWithdrawal(Guid? actorUserId, DateTimeOffset now)
    {
        if (Status != ReportInstanceStatus.Draft || SubmittedAt is null)
        {
            throw new InvalidOperationException($"Can't record a withdrawal for a report in {Status} status.");
        }

        return NextRevision(ReportRevisionAction.Withdrawn, actorUserId, now);
    }

    private ReportRevision NextRevision(ReportRevisionAction action, Guid? actorUserId, DateTimeOffset now)
    {
        RevisionCount++;
        return ReportRevision.Create(Id, RevisionCount, action, AnswersJson, actorUserId, now);
    }

    public void SetFlagged(bool isFlagged) => IsFlagged = isFlagged;

    public void AssignTo(Guid? userId) => AssignedToUserId = userId;
}
