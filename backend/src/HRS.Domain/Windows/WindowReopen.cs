namespace HRS.Domain.Windows;

/// <summary>Reopens an already-closed window for one unit, allowing a late submission up to a new
/// deadline (FR-WIN-006). Owned by SubmissionWindow; a unit can be reopened more than once — the
/// one with the latest ReopenedAt wins.</summary>
public sealed class WindowReopen
{
    private WindowReopen()
    {
    }

    public WindowReopen(Guid unitId, DateTimeOffset reopenedUntil, string reason, DateTimeOffset reopenedAt)
    {
        UnitId = unitId;
        ReopenedUntil = reopenedUntil;
        Reason = reason;
        ReopenedAt = reopenedAt;
    }

    public Guid UnitId { get; private set; }

    public DateTimeOffset ReopenedUntil { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset ReopenedAt { get; private set; }
}
