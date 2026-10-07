using HRS.Domain.Organization;

namespace HRS.Domain.Windows;

/// <summary>
/// When a Report Type's reports open, are due (per level, FR-WIN-003) and hard-close (FR-WIN-001).
/// GraceDays is a duration, not a timestamp — per the "Create window" mockup it's entered as
/// "N days, late but still accepted" and applies on top of each level's own due date, capped by
/// the single window-wide ClosesAt (a level's grace can't outlast the hard close). OpensAt and
/// ClosesAt are window-wide; only DueAt varies by level.
/// </summary>
public sealed class SubmissionWindow
{
    private readonly List<WindowLevelDueDate> _dueDates = [];

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
        DateTimeOffset now)
    {
        Id = id;
        ReportTypeId = reportTypeId;
        PeriodLabel = periodLabel;
        OpensAt = opensAt;
        GraceDays = graceDays;
        ClosesAt = closesAt;
        _dueDates.AddRange(dueDates);
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid ReportTypeId { get; private set; }

    public string PeriodLabel { get; private set; } = string.Empty;

    public DateTimeOffset OpensAt { get; private set; }

    public int GraceDays { get; private set; }

    public DateTimeOffset ClosesAt { get; private set; }

    public IReadOnlyList<WindowLevelDueDate> DueDates => _dueDates;

    public DateTimeOffset CreatedAt { get; private set; }

    public static SubmissionWindow Create(
        Guid id,
        Guid reportTypeId,
        string periodLabel,
        DateTimeOffset opensAt,
        int graceDays,
        DateTimeOffset closesAt,
        IEnumerable<WindowLevelDueDate> dueDates,
        DateTimeOffset now) =>
        new(id, reportTypeId, periodLabel, opensAt, graceDays, closesAt, dueDates, now);

    public void Update(
        string periodLabel,
        DateTimeOffset opensAt,
        int graceDays,
        DateTimeOffset closesAt,
        IEnumerable<WindowLevelDueDate> dueDates)
    {
        PeriodLabel = periodLabel;
        OpensAt = opensAt;
        GraceDays = graceDays;
        ClosesAt = closesAt;
        _dueDates.Clear();
        _dueDates.AddRange(dueDates);
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
