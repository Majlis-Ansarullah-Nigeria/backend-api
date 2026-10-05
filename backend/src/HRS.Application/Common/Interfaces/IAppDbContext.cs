using HRS.Domain.Access;
using HRS.Domain.Invitations;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Invitation> Invitations { get; }

    DbSet<RoleAssignment> RoleAssignments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
