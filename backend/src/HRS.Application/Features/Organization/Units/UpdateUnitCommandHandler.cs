using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Organization.Units;

public sealed class UpdateUnitCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<UpdateUnitCommand, Result<UnitSummary>>
{
    public async ValueTask<Result<UnitSummary>> Handle(UpdateUnitCommand request, CancellationToken cancellationToken)
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

        var level = await db.UnitLevels.AsNoTracking().FirstAsync(l => l.Id == unit.LevelId, cancellationToken);

        unit.Rename(request.Name);
        unit.UpdateContact(request.Address, request.ContactPhone, request.ContactEmail, request.Latitude, request.Longitude);
        await db.SaveChangesAsync(cancellationToken);

        return UnitSummary.FromEntity(unit, level.Code);
    }
}
