using HRS.Domain.Templates;

namespace HRS.Domain.Windows;

/// <summary>
/// A recurring rule that generates SubmissionWindows automatically (FR-WIN-002's own example:
/// "General Monthly Report for month M opens on day 1 of M+1 and is due on day 10"). All of
/// OpensDayOffset, GraceDays, ClosesDayOffset and each level's DueDayOffset are day counts
/// relative to the end of the reporting period being generated — BuildWindow turns a concrete
/// period end into the same (opensAt, graceDays, closesAt, dueDates) shape SubmissionWindow.Create
/// takes, so the generator (Application layer, which owns period-boundary math) just calls this
/// once per period. Reuses ReportTypeFrequency (M3.1) rather than a new enum — a schedule can't
/// be AdHoc frequency (ad-hoc windows are created directly, not scheduled; see SubmissionWindow).
/// </summary>
public sealed class WindowSchedule
{
    private readonly List<ScheduleLevelDueOffset> _dueOffsets = [];

    private WindowSchedule()
    {
    }

    private WindowSchedule(
        Guid id,
        Guid reportTypeId,
        ReportTypeFrequency frequency,
        int opensDayOffset,
        int graceDays,
        int closesDayOffset,
        IEnumerable<ScheduleLevelDueOffset> dueOffsets,
        DateTimeOffset now)
    {
        Id = id;
        ReportTypeId = reportTypeId;
        Frequency = frequency;
        OpensDayOffset = opensDayOffset;
        GraceDays = graceDays;
        ClosesDayOffset = closesDayOffset;
        _dueOffsets.AddRange(dueOffsets);
        IsActive = true;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid ReportTypeId { get; private set; }

    public ReportTypeFrequency Frequency { get; private set; }

    public int OpensDayOffset { get; private set; }

    public int GraceDays { get; private set; }

    public int ClosesDayOffset { get; private set; }

    public IReadOnlyList<ScheduleLevelDueOffset> DueOffsets => _dueOffsets;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static WindowSchedule Create(
        Guid id,
        Guid reportTypeId,
        ReportTypeFrequency frequency,
        int opensDayOffset,
        int graceDays,
        int closesDayOffset,
        IEnumerable<ScheduleLevelDueOffset> dueOffsets,
        DateTimeOffset now) =>
        new(id, reportTypeId, frequency, opensDayOffset, graceDays, closesDayOffset, dueOffsets, now);

    public void Update(
        ReportTypeFrequency frequency,
        int opensDayOffset,
        int graceDays,
        int closesDayOffset,
        IEnumerable<ScheduleLevelDueOffset> dueOffsets)
    {
        Frequency = frequency;
        OpensDayOffset = opensDayOffset;
        GraceDays = graceDays;
        ClosesDayOffset = closesDayOffset;
        _dueOffsets.Clear();
        _dueOffsets.AddRange(dueOffsets);
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    /// <summary>Turns a concrete reporting period's end date into one period's window dates.</summary>
    public (DateTimeOffset OpensAt, int GraceDays, DateTimeOffset ClosesAt, IReadOnlyList<WindowLevelDueDate> DueDates) BuildWindow(
        DateTimeOffset periodEnd)
    {
        var opensAt = periodEnd.AddDays(OpensDayOffset);
        var closesAt = periodEnd.AddDays(ClosesDayOffset);
        var dueDates = _dueOffsets.Select(o => new WindowLevelDueDate(o.Level, periodEnd.AddDays(o.DueDayOffset))).ToList();

        return (opensAt, GraceDays, closesAt, dueDates);
    }
}
