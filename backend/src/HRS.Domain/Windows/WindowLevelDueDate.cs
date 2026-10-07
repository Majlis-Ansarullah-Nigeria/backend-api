using HRS.Domain.Organization;

namespace HRS.Domain.Windows;

/// <summary>One level's due date within a window (FR-WIN-003 — different levels can be due on
/// different days, so parents have time to pull child figures). Owned by SubmissionWindow; has
/// no identity of its own beyond (window, level).</summary>
public sealed class WindowLevelDueDate
{
    private WindowLevelDueDate()
    {
    }

    public WindowLevelDueDate(UnitLevelCode level, DateTimeOffset dueAt)
    {
        Level = level;
        DueAt = dueAt;
    }

    public UnitLevelCode Level { get; private set; }

    public DateTimeOffset DueAt { get; private set; }
}
