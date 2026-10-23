using HRS.Application.Common.Interfaces;
using HRS.Application.Features.Audit;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Organization.Units;

public sealed class MoveUnitCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IAuditLog auditLog)
    : IRequestHandler<MoveUnitCommand, Result<UnitSummary>>
{
    public async ValueTask<Result<UnitSummary>> Handle(MoveUnitCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsOrganizationAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure<UnitSummary>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat or an Admin can manage the organization."));
        }

        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == request.UnitId, cancellationToken);

        if (unit is null)
        {
            return Result.Failure<UnitSummary>(Error.NotFound("UNIT_NOT_FOUND", "Unit not found."));
        }

        if (unit.ParentId is null)
        {
            return Result.Failure<UnitSummary>(Error.Validation("CANNOT_MOVE_ROOT", "The National unit can't be moved."));
        }

        if (unit.ParentId == request.NewParentId)
        {
            return Result.Failure<UnitSummary>(Error.Validation("SAME_PARENT", "The unit is already under this parent."));
        }

        var newParent = await db.Units.FirstOrDefaultAsync(u => u.Id == request.NewParentId, cancellationToken);

        if (newParent is null)
        {
            return Result.Failure<UnitSummary>(Error.Validation("PARENT_NOT_FOUND", "Unknown parent unit."));
        }

        if (newParent.IsArchived)
        {
            return Result.Failure<UnitSummary>(Error.Conflict("PARENT_ARCHIVED", "Can't move a unit under an archived parent."));
        }

        if (newParent.Path == unit.Path || newParent.Path.StartsWith(unit.Path + ".", StringComparison.Ordinal))
        {
            return Result.Failure<UnitSummary>(
                Error.Validation("CANNOT_MOVE_INTO_OWN_SUBTREE", "A unit can't be moved into itself or one of its own descendants."));
        }

        var level = await db.UnitLevels.AsNoTracking().FirstAsync(l => l.Id == unit.LevelId, cancellationToken);
        var newParentLevel = await db.UnitLevels.AsNoTracking().FirstAsync(l => l.Id == newParent.LevelId, cancellationToken);

        if (!UnitHierarchyRules.CanBeChildOf(level.Code, newParentLevel.Code))
        {
            return Result.Failure<UnitSummary>(Error.Validation(
                "INVALID_PARENT_LEVEL", $"A {level.Name} can't be moved under a {newParentLevel.Name} unit."));
        }

        var codeTaken = await db.Units.AnyAsync(
            u => u.ParentId == newParent.Id && u.Code == unit.Code && u.Id != unit.Id, cancellationToken);

        if (codeTaken)
        {
            return Result.Failure<UnitSummary>(
                Error.Conflict("UNIT_CODE_TAKEN", "A unit with this code already exists under the new parent."));
        }

        var oldPath = unit.Path;
        var oldParentName = await db.Units.AsNoTracking().Where(u => u.Id == unit.ParentId).Select(u => u.Name).FirstOrDefaultAsync(cancellationToken);
        var descendants = await db.Units
            .Where(u => u.Path.StartsWith(oldPath + "."))
            .ToListAsync(cancellationToken);

        unit.MoveTo(newParent);

        foreach (var descendant in descendants)
        {
            descendant.RebasePath(unit.Path + descendant.Path[oldPath.Length..]);
        }

        auditLog.Add(new AuditEntry(
            AuditActions.UnitMoved, request.UserId, "unit", unit.Id.ToString(), $"{unit.Name}: {oldParentName} → {newParent.Name}", unit.Id,
            Before: new { Parent = oldParentName, Path = oldPath },
            After: new { Parent = newParent.Name, unit.Path }));
        await db.SaveChangesAsync(cancellationToken);

        return UnitSummary.FromEntity(unit, level.Code);
    }
}
