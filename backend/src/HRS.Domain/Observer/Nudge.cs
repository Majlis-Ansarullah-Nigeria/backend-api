namespace HRS.Domain.Observer;

/// <summary>
/// A record of one reminder sent to an overdue child unit (FR-OBS-003), rate-limited to once per
/// 24 hours per unit — this row's existence within the last 24 hours IS the rate limit; there's no
/// separate counter. Scoped to the unit, not a (unit, window) pair or the sender, matching the
/// SRS's literal "once per 24 hours per unit."
/// </summary>
public sealed class Nudge
{
    private Nudge()
    {
    }

    private Nudge(Guid id, Guid unitId, Guid sentByUserId, DateTimeOffset now)
    {
        Id = id;
        UnitId = unitId;
        SentByUserId = sentByUserId;
        SentAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UnitId { get; private set; }

    public Guid SentByUserId { get; private set; }

    public DateTimeOffset SentAt { get; private set; }

    public static Nudge Create(Guid id, Guid unitId, Guid sentByUserId, DateTimeOffset now) =>
        new(id, unitId, sentByUserId, now);
}
