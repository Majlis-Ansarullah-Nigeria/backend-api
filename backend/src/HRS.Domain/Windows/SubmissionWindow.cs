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
    private readonly List<WindowExtension> _extensions = [];
    private readonly List<WindowReopen> _reopens = [];

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

    public IReadOnlyList<WindowExtension> Extensions => _extensions;

    public IReadOnlyList<WindowReopen> Reopens => _reopens;

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

    /// <summary>A unit's own due date for its level — an active extension (BR-WIN-05) overrides
    /// the level's default for that unit only.</summary>
    public DateTimeOffset? DueAtFor(UnitLevelCode level, Guid? unitId = null)
    {
        var extensionDueAt = unitId is null ? null : LatestExtensionFor(unitId.Value)?.NewDueAt;
        return extensionDueAt ?? _dueDates.FirstOrDefault(d => d.Level == level)?.DueAt;
    }

    /// <summary>A unit's own hard close — an active extension overrides the window-wide ClosesAt
    /// for that unit only (BR-WIN-05).</summary>
    public DateTimeOffset EffectiveClosesAtFor(Guid unitId) => LatestExtensionFor(unitId)?.NewClosesAt ?? ClosesAt;

    /// <summary>The last moment a late submission is still accepted for this level — the level's
    /// own due date plus the grace duration, but never later than the unit's effective hard
    /// close.</summary>
    public DateTimeOffset? GraceEndFor(UnitLevelCode level, Guid? unitId = null)
    {
        var dueAt = DueAtFor(level, unitId);
        if (dueAt is null)
        {
            return null;
        }

        var closesAt = unitId is null ? ClosesAt : EffectiveClosesAtFor(unitId.Value);
        var graceEnd = dueAt.Value.AddDays(GraceDays);
        return graceEnd < closesAt ? graceEnd : closesAt;
    }

    public WindowLevelStatus StatusFor(UnitLevelCode level, DateTimeOffset now, Guid? unitId = null)
    {
        var dueAt = DueAtFor(level, unitId);
        var closesAt = unitId is null ? ClosesAt : EffectiveClosesAtFor(unitId.Value);

        if (now < OpensAt)
        {
            return WindowLevelStatus.BeforeOpen;
        }

        if (dueAt is null || now >= closesAt)
        {
            var reopen = unitId is null ? null : LatestReopenFor(unitId.Value);
            return reopen is not null && now < reopen.ReopenedUntil ? WindowLevelStatus.Late : WindowLevelStatus.Closed;
        }

        if (now < dueAt.Value)
        {
            return WindowLevelStatus.OnTime;
        }

        var graceEnd = GraceEndFor(level, unitId)!.Value;
        return now < graceEnd ? WindowLevelStatus.Late : WindowLevelStatus.Closed;
    }

    /// <summary>Grants a new due date and close date to one unit or a group (FR-WIN-005). Each
    /// unit gets its own record so a later extension to one unit never touches another's.</summary>
    public void GrantExtension(IEnumerable<Guid> unitIds, DateTimeOffset newDueAt, DateTimeOffset newClosesAt, string reason, DateTimeOffset now)
    {
        foreach (var unitId in unitIds)
        {
            _extensions.Add(new WindowExtension(unitId, newDueAt, newClosesAt, reason, now));
        }
    }

    /// <summary>Reopens this window for one unit or a group after it has already closed, allowing
    /// a late submission up to a new deadline (FR-WIN-006).</summary>
    public void Reopen(IEnumerable<Guid> unitIds, DateTimeOffset reopenedUntil, string reason, DateTimeOffset now)
    {
        foreach (var unitId in unitIds)
        {
            _reopens.Add(new WindowReopen(unitId, reopenedUntil, reason, now));
        }
    }

    private WindowExtension? LatestExtensionFor(Guid unitId) =>
        _extensions.Where(e => e.UnitId == unitId).MaxBy(e => e.GrantedAt);

    private WindowReopen? LatestReopenFor(Guid unitId) =>
        _reopens.Where(r => r.UnitId == unitId).MaxBy(r => r.ReopenedAt);
}
