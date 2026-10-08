using HRS.Domain.Access;
using HRS.Domain.Invitations;
using HRS.Domain.Organization;
using HRS.Domain.Templates;
using HRS.Domain.Windows;
using Microsoft.EntityFrameworkCore;

namespace HRS.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Invitation> Invitations { get; }

    DbSet<ReportType> ReportTypes { get; }

    DbSet<RoleAssignment> RoleAssignments { get; }

    DbSet<SubmissionWindow> SubmissionWindows { get; }

    DbSet<TemplateVersion> TemplateVersions { get; }

    DbSet<UnitLevel> UnitLevels { get; }

    DbSet<Unit> Units { get; }

    DbSet<WindowJobRun> WindowJobRuns { get; }

    DbSet<WindowSchedule> WindowSchedules { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
