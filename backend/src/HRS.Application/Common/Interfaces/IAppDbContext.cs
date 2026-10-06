using HRS.Domain.Access;
using HRS.Domain.Invitations;
using HRS.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Invitation> Invitations { get; }

    DbSet<RoleAssignment> RoleAssignments { get; }

    DbSet<UnitLevel> UnitLevels { get; }

    DbSet<Unit> Units { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
