namespace HRS.Domain.Windows;

/// <summary>An override of a window's due and close dates for one unit (FR-WIN-005, BR-WIN-05).
/// Owned by SubmissionWindow; a unit can be granted more than one extension over time — the one
/// with the latest GrantedAt wins.</summary>
public sealed class WindowExtension
{
    private WindowExtension()
    {
    }

    public WindowExtension(Guid unitId, DateTimeOffset newDueAt, DateTimeOffset newClosesAt, string reason, DateTimeOffset grantedAt)
    {
        UnitId = unitId;
        NewDueAt = newDueAt;
        NewClosesAt = newClosesAt;
        Reason = reason;
        GrantedAt = grantedAt;
    }

    public Guid UnitId { get; private set; }

    public DateTimeOffset NewDueAt { get; private set; }

    public DateTimeOffset NewClosesAt { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset GrantedAt { get; private set; }
}
