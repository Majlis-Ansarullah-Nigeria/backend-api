namespace HRS.Domain.Review;

/// <summary>
/// A Secretariat user's saved inbox filter combination (FR-REV-001). Personal to the user who
/// created it — not shared across the Secretariat team, the simplest reading that avoids any
/// ownership/edit-conflict questions a shared view would raise. FilterJson is opaque here for the
/// same reason AnswersJson is on ReportInstance: the filter shape belongs to Application.
/// </summary>
public sealed class SavedView
{
    private SavedView()
    {
    }

    private SavedView(Guid id, Guid userId, string name, string filterJson, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        Name = name;
        FilterJson = filterJson;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string FilterJson { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static SavedView Create(Guid id, Guid userId, string name, string filterJson, DateTimeOffset now) =>
        new(id, userId, name, filterJson, now);
}
