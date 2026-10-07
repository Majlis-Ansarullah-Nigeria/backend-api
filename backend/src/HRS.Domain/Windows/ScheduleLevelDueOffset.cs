using HRS.Domain.Organization;

namespace HRS.Domain.Windows;

/// <summary>One level's due-date rule within a schedule — a day offset, not an absolute date,
/// e.g. "due 10 days after the reporting period ends" (FR-WIN-002's own example: "due on day 10").
/// Owned by WindowSchedule; no identity of its own beyond (schedule, level).</summary>
public sealed class ScheduleLevelDueOffset
{
    private ScheduleLevelDueOffset()
    {
    }

    public ScheduleLevelDueOffset(UnitLevelCode level, int dueDayOffset)
    {
        Level = level;
        DueDayOffset = dueDayOffset;
    }

    public UnitLevelCode Level { get; private set; }

    /// <summary>Days after the reporting period ends that this level's report is due.</summary>
    public int DueDayOffset { get; private set; }
}
