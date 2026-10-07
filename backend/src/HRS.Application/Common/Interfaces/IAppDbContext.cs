using HRS.Domain.Access;
using HRS.Domain.Invitations;
using HRS.Domain.Organization;
using HRS.Domain.Templates;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Invitation> Invitations { get; }

    DbSet<ReportType> ReportTypes { get; }

    DbSet<RoleAssignment> RoleAssignments { get; }

    DbSet<TemplateVersion> TemplateVersions { get; }

    DbSet<UnitLevel> UnitLevels { get; }

    DbSet<Unit> Units { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
