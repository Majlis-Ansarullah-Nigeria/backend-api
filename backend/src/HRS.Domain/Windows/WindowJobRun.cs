namespace HRS.Domain.Windows;

/// <summary>Records that a window-lifecycle event (BR-WIN-08) has already fired for one unit, so
/// re-running the worker never fires it twice. Not an owned child of SubmissionWindow — it's
/// per-unit, grows without bound, and is queried independently (by worker, and later by M8's
/// dispatcher), so it gets its own table rather than living in SubmissionWindow's aggregate.
/// EventKey disambiguates event types that can legitimately fire more than once for the same
/// (window, unit) — the three reminder days ("5"/"2"/"1") — and is empty for the others, which
/// only ever fire once each.</summary>
public sealed class WindowJobRun
{
    private WindowJobRun()
    {
    }

    public WindowJobRun(Guid id, Guid windowId, Guid unitId, WindowJobEventType eventType, string eventKey, DateTimeOffset occurredAt)
    {
        Id = id;
        WindowId = windowId;
        UnitId = unitId;
        EventType = eventType;
        EventKey = eventKey;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public Guid WindowId { get; private set; }

    public Guid UnitId { get; private set; }

    public WindowJobEventType EventType { get; private set; }

    public string EventKey { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }
}
