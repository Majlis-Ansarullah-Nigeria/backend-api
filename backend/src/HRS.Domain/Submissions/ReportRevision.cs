namespace HRS.Domain.Submissions;

/// <summary>
/// One immutable entry in a report's version history (FR-HIS-006, SRS §8 data dictionary): the
/// full answers snapshot at the moment of a submission, withdrawal or resubmission. Diffs are
/// computed between consecutive snapshots at read time, never stored. Created only through
/// <see cref="ReportInstance.RecordSubmission"/>/<see cref="ReportInstance.RecordWithdrawal"/>,
/// which own the revision numbering and the status guards. Append-only — no edit or delete.
/// </summary>
public sealed class ReportRevision
{
    private ReportRevision()
    {
    }

    private ReportRevision(Guid id, Guid reportInstanceId, int revisionNo, ReportRevisionAction action, string answersJson, Guid? actorUserId, DateTimeOffset now)
    {
        Id = id;
        ReportInstanceId = reportInstanceId;
        RevisionNo = revisionNo;
        Action = action;
        AnswersJson = answersJson;
        ActorUserId = actorUserId;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid ReportInstanceId { get; private set; }

    /// <summary>1-based, gapless per report instance.</summary>
    public int RevisionNo { get; private set; }

    public ReportRevisionAction Action { get; private set; }

    public string AnswersJson { get; private set; } = "{}";

    /// <summary>Null only for snapshots backfilled from reports submitted before this history
    /// existed without a recorded submitter. No FK, same as ReportInstance.SubmittedByUserId.</summary>
    public Guid? ActorUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static ReportRevision Create(Guid reportInstanceId, int revisionNo, ReportRevisionAction action, string answersJson, Guid? actorUserId, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), reportInstanceId, revisionNo, action, answersJson, actorUserId, now);
}
