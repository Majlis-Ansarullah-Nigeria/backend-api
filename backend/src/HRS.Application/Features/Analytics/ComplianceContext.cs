using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Organization.Units;
using HRS.Application.Features.Submissions.MyReports;
using HRS.Domain.Organization;
using HRS.Domain.Submissions;
using HRS.Domain.Windows;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Analytics;

/// <summary>
/// Everything the compliance views (FR-DSH-001) need to place each expected unit of a window in
/// a bucket and a group, loaded once per request. Expected units follow the same rule as the
/// window jobs (WindowJobRunner): a window's hand-picked TargetUnitIds when set, otherwise every
/// active unit at a level the window has a due date for.
/// </summary>
public sealed class ComplianceContext
{
    public sealed record UnitEntry(Guid Id, string Name, UnitLevelCode Level, string Path);

    public sealed record UnitOutcome(UnitEntry Unit, ReportLineStatus Status, ComplianceBucket Bucket, ReportInstance? Instance);

    public sealed record Group(Guid? GroupUnitId, string Name);

    private readonly IReadOnlyList<UnitEntry> _units;
    private readonly IReadOnlyDictionary<string, Unit> _unitByPath;
    private readonly Dictionary<(Guid WindowId, Guid UnitId), ReportInstance> _instances;

    private ComplianceContext(
        IReadOnlyList<UnitEntry> units,
        IReadOnlyDictionary<string, Unit> unitByPath,
        Dictionary<(Guid, Guid), ReportInstance> instances,
        IReadOnlyDictionary<UnitLevelCode, UnitLevel> levels)
    {
        _units = units;
        _unitByPath = unitByPath;
        _instances = instances;
        Levels = levels;
    }

    public IReadOnlyDictionary<UnitLevelCode, UnitLevel> Levels { get; }

    public IReadOnlyList<UnitEntry> Units => _units;

    public static async Task<ComplianceContext> LoadAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> windowIds, CancellationToken cancellationToken)
    {
        var levels = await db.UnitLevels.AsNoTracking().ToListAsync(cancellationToken);
        var levelById = levels.ToDictionary(l => l.Id);
        var activeUnits = await db.Units.AsNoTracking().Where(u => u.ArchivedAt == null).ToListAsync(cancellationToken);

        var units = activeUnits
            .Select(u => new UnitEntry(u.Id, u.Name, levelById[u.LevelId].Code, u.Path))
            .ToList();

        var instances = windowIds.Count == 0
            ? []
            : await db.ReportInstances.AsNoTracking()
                .Where(i => windowIds.Contains(i.WindowId))
                .ToDictionaryAsync(i => (i.WindowId, i.UnitId), cancellationToken);

        return new ComplianceContext(
            units, activeUnits.ToDictionary(u => u.Path), instances, levels.ToDictionary(l => l.Code));
    }

    public IEnumerable<UnitEntry> ExpectedUnits(SubmissionWindow window, UnitLevelCode? level = null)
    {
        var targets = window.TargetUnitIds.Count > 0 ? window.TargetUnitIds.ToHashSet() : null;
        return _units.Where(u =>
            (level is null || u.Level == level)
            && (targets is null || targets.Contains(u.Id))
            && window.DueAtFor(u.Level, u.Id) is not null);
    }

    public UnitOutcome Outcome(SubmissionWindow window, UnitEntry unit, DateTimeOffset now)
    {
        var instance = _instances.GetValueOrDefault((window.Id, unit.Id));
        var status = MyReportStatusCalculator.Determine(window, instance, unit.Level, unit.Id, now);
        return new UnitOutcome(unit, status, ComplianceBuckets.Classify(status, instance), instance);
    }

    /// <summary>The compliance table's rows: a unit's zone, or the department itself for
    /// Department units (which hang off National, outside every zone).</summary>
    public Group GroupOf(UnitEntry unit)
    {
        if (unit.Level == UnitLevelCode.Department)
        {
            return new Group(unit.Id, unit.Name);
        }

        var (zoneId, zoneName) = ZoneResolver.Resolve(unit.Path, unit.Level, _unitByPath);
        return new Group(zoneId, zoneName ?? unit.Name);
    }

    /// <summary>Level order for tabs and the by-level list — lowest level first, as in the mockups.</summary>
    public static readonly UnitLevelCode[] DisplayOrder =
        [UnitLevelCode.Muqam, UnitLevelCode.Dila, UnitLevelCode.Zone, UnitLevelCode.Department, UnitLevelCode.National];
}
