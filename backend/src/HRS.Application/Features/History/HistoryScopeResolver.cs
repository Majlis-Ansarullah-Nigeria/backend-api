using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.History;

/// <summary>
/// FR-HIS-001's "every unit they head or observe" — identical scope rule to
/// GetComplianceGridQuery/ChildReportResolver (FR-OBS-002): own units get every report type,
/// observed units (direct children of a headed unit) only report types with ObserverDepth &gt;= 1
/// at that child's level.
/// </summary>
public static class HistoryScopeResolver
{
    public sealed record Scope(HashSet<Guid> UnitIds, HashSet<Guid> ObservedUnitIds);

    public static async Task<Scope> ResolveAsync(IAppDbContext db, Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var headUnitIds = await db.RoleAssignments.AsNoTracking()
            .Where(a => a.UserId == userId && a.Role == RoleName.UnitHead
                && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .Select(a => a.UnitId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (headUnitIds.Count == 0)
        {
            return new Scope([], []);
        }

        var children = await db.Units.AsNoTracking()
            .Where(u => u.ParentId != null && headUnitIds.Contains(u.ParentId.Value) && u.ArchivedAt == null)
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { u.Id, LevelCode = l.Code })
            .ToListAsync(cancellationToken);

        if (children.Count == 0)
        {
            return new Scope([.. headUnitIds], []);
        }

        var reportTypes = await db.ReportTypes.AsNoTracking().Where(r => r.ArchivedAt == null).ToListAsync(cancellationToken);
        var observableLevels = reportTypes
            .Where(r => r.ObserverDepth >= 1)
            .SelectMany(r => r.ApplicableLevels)
            .ToHashSet();

        var observedUnitIds = children.Where(c => observableLevels.Contains(c.LevelCode)).Select(c => c.Id).ToHashSet();

        var unitIds = new HashSet<Guid>(headUnitIds);
        unitIds.UnionWith(observedUnitIds);

        return new Scope(unitIds, observedUnitIds);
    }
}
