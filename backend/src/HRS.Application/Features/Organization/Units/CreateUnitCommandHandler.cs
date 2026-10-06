using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using HRS.Domain.Organization;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Unit = HRS.Domain.Organization.Unit;

namespace HRS.Application.Features.Organization.Units;

public sealed class CreateUnitCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<CreateUnitCommand, Result<UnitSummary>>
{
    public async ValueTask<Result<UnitSummary>> Handle(CreateUnitCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsOrganizationAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure<UnitSummary>(
                Error.Forbidden("FORBIDDEN", "Only the Secretariat or an Admin can manage the organization."));
        }

        var level = await db.UnitLevels.AsNoTracking().FirstOrDefaultAsync(l => l.Id == request.LevelId, cancellationToken);

        if (level is null)
        {
            return Result.Failure<UnitSummary>(Error.Validation("LEVEL_NOT_FOUND", "Unknown unit level."));
        }

        var parent = await db.Units.FirstOrDefaultAsync(u => u.Id == request.ParentId, cancellationToken);

        if (parent is null)
        {
            return Result.Failure<UnitSummary>(Error.Validation("PARENT_NOT_FOUND", "Unknown parent unit."));
        }

        if (parent.IsArchived)
        {
            return Result.Failure<UnitSummary>(Error.Conflict("PARENT_ARCHIVED", "Can't add a unit under an archived parent."));
        }

        var parentLevel = await db.UnitLevels.AsNoTracking().FirstAsync(l => l.Id == parent.LevelId, cancellationToken);

        if (!UnitHierarchyRules.CanBeChildOf(level.Code, parentLevel.Code))
        {
            return Result.Failure<UnitSummary>(Error.Validation(
                "INVALID_PARENT_LEVEL", $"A {level.Name} can't be created under a {parentLevel.Name} unit."));
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var codeTaken = await db.Units.AnyAsync(
            u => u.ParentId == parent.Id && u.Code == normalizedCode, cancellationToken);

        if (codeTaken)
        {
            return Result.Failure<UnitSummary>(
                Error.Conflict("UNIT_CODE_TAKEN", "A unit with this code already exists under the same parent."));
        }

        var unit = Unit.Create(
            Guid.CreateVersion7(),
            request.Code,
            request.Name,
            level.Id,
            parent,
            request.Address,
            request.ContactPhone,
            request.ContactEmail,
            request.Latitude,
            request.Longitude,
            clock.UtcNow);

        db.Units.Add(unit);
        await db.SaveChangesAsync(cancellationToken);

        return UnitSummary.FromEntity(unit, level.Code);
    }
}
