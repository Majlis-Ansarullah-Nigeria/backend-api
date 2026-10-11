namespace HRS.Domain.Observer;

/// <summary>
/// An observer's private note on a child report (FR-OBS-004). Scoped to the observing UNIT, not
/// the individual author — "visible only to themselves and their successors in office" (SRS) means
/// whoever currently holds UnitHead at ObserverUnitId sees every note ever written there, the same
/// way everything else in this app ties access to a unit rather than a person (hrs-domain: "Reports
/// belong to a Unit, never to a User"). Append-only — the mockup shows a simple timestamped log,
/// no edit or delete.
/// </summary>
public sealed class PrivateNote
{
    private PrivateNote()
    {
    }

    private PrivateNote(Guid id, Guid reportInstanceId, Guid observerUnitId, Guid authorUserId, string body, DateTimeOffset now)
    {
        Id = id;
        ReportInstanceId = reportInstanceId;
        ObserverUnitId = observerUnitId;
        AuthorUserId = authorUserId;
        Body = body;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid ReportInstanceId { get; private set; }

    public Guid ObserverUnitId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static PrivateNote Create(Guid id, Guid reportInstanceId, Guid observerUnitId, Guid authorUserId, string body, DateTimeOffset now) =>
        new(id, reportInstanceId, observerUnitId, authorUserId, body, now);
}
