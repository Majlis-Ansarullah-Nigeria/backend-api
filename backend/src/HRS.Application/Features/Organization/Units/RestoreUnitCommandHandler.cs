using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Organization.Units;

public sealed class RestoreUnitCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy)
    : IRequestHandler<RestoreUnitCommand, Result>
{
    public async ValueTask<Result> Handle(RestoreUnitCommand request, CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsOrganizationAdminAsync(request.UserId, cancellationToken))
        {
            return Result.Failure(Error.Forbidden("FORBIDDEN", "Only the Secretariat or an Admin can manage the organization."));
        }

        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == request.UnitId, cancellationToken);

        if (unit is null)
        {
            return Result.Failure(Error.NotFound("UNIT_NOT_FOUND", "Unit not found."));
        }

        unit.Restore();
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
