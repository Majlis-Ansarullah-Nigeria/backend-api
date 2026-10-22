using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Analytics;

/// <summary>
/// Which parts of the tree a user's dashboards cover (SRS §3.2 "National dashboards"): the
/// Secretariat and Leadership see the whole organization; a Zone or Dila head sees only their own
/// unit's subtree (FR-DSH-005). Everyone else has no dashboard.
/// </summary>
public static class DashboardScope
{
    public sealed record Scope(bool IsNational, bool IsSecretariat, IReadOnlyList<IndicatorCalculator.UnitNode> Roots)
    {
        public bool HasAccess => Roots.Count > 0;

        public IndicatorCalculator.UnitNode? RootContaining(IndicatorCalculator.UnitNode unit) =>
            Roots.FirstOrDefault(root => unit.Path == root.Path || unit.Path.StartsWith(root.Path + ".", StringComparison.Ordinal));
    }

    public static async Task<Scope> ResolveAsync(
        IAppDbContext db, IAccessPolicy accessPolicy, Guid userId, IReadOnlyCollection<IndicatorCalculator.UnitNode> units, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var isSecretariat = await accessPolicy.HasRoleAsync(userId, RoleName.Secretariat, cancellationToken);
        if (isSecretariat || await accessPolicy.HasRoleAsync(userId, RoleName.Leadership, cancellationToken))
        {
            var national = units.FirstOrDefault(u => u.Level == UnitLevelCode.National && u.ParentId is null);
            return new Scope(true, isSecretariat, national is null ? [] : [national]);
        }

        var headedUnitIds = await db.RoleAssignments.AsNoTracking()
            .Where(a => a.UserId == userId && a.Role == RoleName.UnitHead && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .Select(a => a.UnitId)
            .ToListAsync(cancellationToken);

        var roots = units
            .Where(u => headedUnitIds.Contains(u.Id) && (u.Level is UnitLevelCode.Zone or UnitLevelCode.Dila))
            .OrderBy(u => u.Level)
            .ThenBy(u => u.Name, StringComparer.Ordinal)
            .ToList();
        return new Scope(false, false, roots);
    }

    /// <summary>The node a dashboard request opens: the one asked for, or the user's first root.
    /// Refused when it's outside every root the user's scope covers (FR-DSH-005).</summary>
    public static Result<(IndicatorCalculator.UnitNode Node, IndicatorCalculator.UnitNode Root)> ResolveNode(
        Scope scope, IReadOnlyList<IndicatorCalculator.UnitNode> units, Guid? unitId)
    {
        var node = unitId is { } id ? units.FirstOrDefault(u => u.Id == id) : scope.Roots[0];
        if (node is null)
        {
            return Result.Failure<(IndicatorCalculator.UnitNode, IndicatorCalculator.UnitNode)>(Error.NotFound("UNIT_NOT_FOUND", "That unit doesn't exist."));
        }

        return scope.RootContaining(node) is { } root
            ? Result.Success((node, root))
            : Result.Failure<(IndicatorCalculator.UnitNode, IndicatorCalculator.UnitNode)>(Error.Forbidden("FORBIDDEN", "That unit is outside your dashboard."));
    }

    public static async Task<IReadOnlyList<IndicatorCalculator.UnitNode>> LoadUnitsAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var levels = await db.UnitLevels.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Code, cancellationToken);
        var units = await db.Units.AsNoTracking()
            .Where(u => u.ArchivedAt == null)
            .Select(u => new { u.Id, u.ParentId, u.Name, u.LevelId, u.Path })
            .ToListAsync(cancellationToken);
        return [.. units.Select(u => new IndicatorCalculator.UnitNode(u.Id, u.ParentId, u.Name, levels[u.LevelId], u.Path))];
    }
}
