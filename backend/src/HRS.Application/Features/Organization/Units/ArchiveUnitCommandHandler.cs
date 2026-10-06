using HRS.Application.Common.Interfaces;
using HRS.Domain.Common;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Features.Organization.Units;

public sealed class ArchiveUnitCommandHandler(IAppDbContext db, IAccessPolicy accessPolicy, IClock clock)
    : IRequestHandler<ArchiveUnitCommand, Result>
{
    public async ValueTask<Result> Handle(ArchiveUnitCommand request, CancellationToken cancellationToken)
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

        if (unit.ParentId is null)
        {
            return Result.Failure(Error.Validation("CANNOT_ARCHIVE_ROOT", "The National unit can't be archived."));
        }

        unit.Archive(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
