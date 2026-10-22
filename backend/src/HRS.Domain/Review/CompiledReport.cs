namespace HRS.Domain.Review;

/// <summary>
/// FR-REV-006's compiled national report for one window. The figures are never stored — they're
/// recomputed from acknowledged reports on every view, so the report refreshes as more reports
/// are acknowledged. Only the Secretariat's narrative (the "narrative highlights") lives here.
/// </summary>
public sealed class CompiledReport
{
    public const int MaxNarrativeLength = 20_000;

    private CompiledReport()
    {
    }

    private CompiledReport(Guid id, Guid windowId)
    {
        Id = id;
        WindowId = windowId;
    }

    public Guid Id { get; private set; }

    public Guid WindowId { get; private set; }

    public string Narrative { get; private set; } = string.Empty;

    public DateTimeOffset? NarrativeUpdatedAt { get; private set; }

    public Guid? NarrativeUpdatedByUserId { get; private set; }

    public static CompiledReport Create(Guid id, Guid windowId) => new(id, windowId);

    public void UpdateNarrative(string narrative, Guid userId, DateTimeOffset now)
    {
        if (narrative.Length > MaxNarrativeLength)
        {
            throw new ArgumentException($"The narrative can't be longer than {MaxNarrativeLength} characters.", nameof(narrative));
        }

        Narrative = narrative;
        NarrativeUpdatedAt = now;
        NarrativeUpdatedByUserId = userId;
    }
}
