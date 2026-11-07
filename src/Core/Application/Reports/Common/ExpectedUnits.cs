using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Reports.Common;

/// <summary>A unit that is expected to submit a report at a level, with its place in the tree.</summary>
public record ExpectedUnit(OrganizationLevel Level, Guid UnitId, string Name, Guid? MuqamId, Guid? DilaId, Guid? ZoneId);

public static class ExpectedUnits
{
    /// <summary>Every Muqam, Dila or Zone expected to report for a template at that level.</summary>
    public static async Task<List<ExpectedUnit>> ForLevelAsync(
        IApplicationDbContext context,
        OrganizationLevel level,
        CancellationToken cancellationToken) => level switch
    {
        OrganizationLevel.Muqam => await context.Muqams.AsNoTracking()
            .Select(m => new ExpectedUnit(OrganizationLevel.Muqam, m.Id, m.Name, m.Id, m.DilaId, m.Dila != null ? m.Dila.ZoneId : null))
            .ToListAsync(cancellationToken),
        OrganizationLevel.Dila => await context.Dilas.AsNoTracking()
            .Select(d => new ExpectedUnit(OrganizationLevel.Dila, d.Id, d.Name, null, d.Id, d.ZoneId))
            .ToListAsync(cancellationToken),
        OrganizationLevel.Zone => await context.Zones.AsNoTracking()
            .Select(z => new ExpectedUnit(OrganizationLevel.Zone, z.Id, z.Name, null, null, z.Id))
            .ToListAsync(cancellationToken),
        _ => new List<ExpectedUnit>()
    };

    /// <summary>Every unit expected to report for a template filled at several levels.</summary>
    public static async Task<List<ExpectedUnit>> ForLevelsAsync(
        IApplicationDbContext context,
        IEnumerable<OrganizationLevel> levels,
        CancellationToken cancellationToken)
    {
        var all = new List<ExpectedUnit>();
        foreach (var level in levels.Where(IsUnitLevel).Distinct())
        {
            all.AddRange(await ForLevelAsync(context, level, cancellationToken));
        }

        return all;
    }

    public static bool IsUnitLevel(OrganizationLevel level) =>
        level is OrganizationLevel.Muqam or OrganizationLevel.Dila or OrganizationLevel.Zone;
}
