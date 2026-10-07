using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Windows.SubmissionWindows;

/// <summary>Validates FR-WIN-004's "hand-picked list of units": every target unit must exist, be
/// active, and be at a level this window already has a due date for (a target unit at a level
/// with no due date would never know when it's due).</summary>
public static class TargetUnitValidator
{
    public static async Task<Error?> ValidateAsync(
        IAppDbContext db, IReadOnlyList<Guid>? targetUnitIds, IReadOnlyList<WindowDueDate> dueDates, CancellationToken cancellationToken)
    {
        if (targetUnitIds is null || targetUnitIds.Count == 0)
        {
            return null;
        }

        var dueDateLevels = dueDates.Select(d => d.Level).ToHashSet();

        var units = await db.Units
            .AsNoTracking()
            .Where(u => targetUnitIds.Contains(u.Id))
            .Select(u => new { u.Id, u.LevelId, u.ArchivedAt })
            .ToListAsync(cancellationToken);

        var foundIds = units.Select(u => u.Id).ToHashSet();
        var missingIds = targetUnitIds.Where(id => !foundIds.Contains(id)).ToList();
        if (missingIds.Count > 0)
        {
            return Error.Validation("TARGET_UNIT_NOT_FOUND", $"Unit {missingIds[0]} not found.");
        }

        var archivedUnit = units.FirstOrDefault(u => u.ArchivedAt is not null);
        if (archivedUnit is not null)
        {
            return Error.Validation("TARGET_UNIT_ARCHIVED", $"Unit {archivedUnit.Id} is archived.");
        }

        var levelByUnitId = await db.Units
            .AsNoTracking()
            .Where(u => targetUnitIds.Contains(u.Id))
            .Join(db.UnitLevels.AsNoTracking(), u => u.LevelId, l => l.Id, (u, l) => new { u.Id, l.Code })
            .ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);

        var unitsWithNoDueDate = targetUnitIds.Where(id => !dueDateLevels.Contains(levelByUnitId[id])).ToList();
        return unitsWithNoDueDate.Count > 0
            ? Error.Validation("TARGET_UNIT_LEVEL_HAS_NO_DUE_DATE", $"Unit {unitsWithNoDueDate[0]}'s level has no due date set in this window.")
            : null;
    }
}
