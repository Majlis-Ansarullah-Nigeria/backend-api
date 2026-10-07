using HRS.Domain.Organization;

namespace HRS.Domain.Windows;

/// <summary>
/// When a Report Type's reports open, are due (per level, FR-WIN-003) and hard-close (FR-WIN-001).
/// GraceDays is a duration, not a timestamp — per the "Create window" mockup it's entered as
/// "N days, late but still accepted" and applies on top of each level's own due date, capped by
/// the single window-wide ClosesAt (a level's grace can't outlast the hard close). OpensAt and
/// ClosesAt are window-wide; only DueAt varies by level.
///
/// ScheduleId (M4.2) records which WindowSchedule generated this window, if any — null means it
/// was created directly, which is what "ad-hoc" (FR-WIN-004) means here; there's no separate
/// IsAdHoc flag. TargetUnitIds (M4.2), when set, is FR-WIN-004's "hand-picked list of units" —
/// it restricts the window to exactly those units on top of (not instead of) the per-level due
/// dates, which still say when each affected level is due.
/// </summary>
public sealed class SubmissionWindow
{
    private readonly List<WindowLevelDueDate> _dueDates = [];
    private readonly List<Guid> _targetUnitIds = [];

    private SubmissionWindow()
    {
    }

    private SubmissionWindow(
        Guid id,
        Guid reportTypeId,
        string periodLabel,
        DateTimeOffset opensAt,
        int graceDays,
        DateTimeOffset closesAt,
        IEnumerable<WindowLevelDueDate> dueDates,
        DateTimeOffset now,
        Guid? scheduleId,
        IEnumerable<Guid>? targetUnitIds)
    {
        Id = id;
        ReportTypeId = reportTypeId;
        PeriodLabel = periodLabel;
        OpensAt = opensAt;
        GraceDays = graceDays;
        ClosesAt = closesAt;
        _dueDates.AddRange(dueDates);
        CreatedAt = now;
        ScheduleId = scheduleId;
        _targetUnitIds.AddRange(targetUnitIds ?? []);
    }

    public Guid Id { get; private set; }

    public Guid ReportTypeId { get; private set; }

    public string PeriodLabel { get; private set; } = string.Empty;

    public DateTimeOffset OpensAt { get; private set; }

    public int GraceDays { get; private set; }

    public DateTimeOffset ClosesAt { get; private set; }

    public IReadOnlyList<WindowLevelDueDate> DueDates => _dueDates;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? ScheduleId { get; private set; }

    public IReadOnlyList<Guid> TargetUnitIds => _targetUnitIds;

    public static SubmissionWindow Create(
        Guid id,
        Guid reportTypeId,
        string periodLabel,
        DateTimeOffset opensAt,
        int graceDays,
        DateTimeOffset closesAt,
        IEnumerable<WindowLevelDueDate> dueDates,
        DateTimeOffset now,
        Guid? scheduleId = null,
        IEnumerable<Guid>? targetUnitIds = null) =>
        new(id, reportTypeId, periodLabel, opensAt, graceDays, closesAt, dueDates, now, scheduleId, targetUnitIds);

    public void Update(
        string periodLabel,
        DateTimeOffset opensAt,
        int graceDays,
        DateTimeOffset closesAt,
        IEnumerable<WindowLevelDueDate> dueDates,
        IEnumerable<Guid>? targetUnitIds = null)
    {
        PeriodLabel = periodLabel;
        OpensAt = opensAt;
        GraceDays = graceDays;
        ClosesAt = closesAt;
        _dueDates.Clear();
        _dueDates.AddRange(dueDates);
        _targetUnitIds.Clear();
        _targetUnitIds.AddRange(targetUnitIds ?? []);
    }

    public DateTimeOffset? DueAtFor(UnitLevelCode level) =>
        _dueDates.FirstOrDefault(d => d.Level == level)?.DueAt;

    /// <summary>The last moment a late submission is still accepted for this level — the level's
    /// own due date plus the grace duration, but never later than the window's hard close.</summary>
    public DateTimeOffset? GraceEndFor(UnitLevelCode level)
    {
        var dueAt = DueAtFor(level);
        if (dueAt is null)
        {
            return null;
        }

        var graceEnd = dueAt.Value.AddDays(GraceDays);
        return graceEnd < ClosesAt ? graceEnd : ClosesAt;
    }

    public WindowLevelStatus StatusFor(UnitLevelCode level, DateTimeOffset now)
    {
        var dueAt = DueAtFor(level);

        if (now < OpensAt)
        {
            return WindowLevelStatus.BeforeOpen;
        }

        if (dueAt is null || now >= ClosesAt)
        {
            return WindowLevelStatus.Closed;
        }

        if (now < dueAt.Value)
        {
            return WindowLevelStatus.OnTime;
        }

        var graceEnd = GraceEndFor(level)!.Value;
        return now < graceEnd ? WindowLevelStatus.Late : WindowLevelStatus.Closed;
    }
}
