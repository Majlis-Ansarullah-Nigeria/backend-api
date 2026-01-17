using ManagementApi.Domain.Entities;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Member> Members { get; }
    DbSet<MemberPosition> MemberPositions { get; }
    DbSet<Muqam> Muqams { get; }
    DbSet<Dila> Dilas { get; }
    DbSet<Zone> Zones { get; }
    DbSet<Jamaat> Jamaats { get; }
    DbSet<ApplicationUser> Users { get; }
    DbSet<ApplicationRole> Roles { get; }
    DbSet<ApplicationRolePermission> RolePermissions { get; }

    // Reports
    DbSet<ReportTemplate> ReportTemplates { get; }
    DbSet<ReportSection> ReportSections { get; }
    DbSet<ReportQuestion> ReportQuestions { get; }
    DbSet<ReportSubmission> ReportSubmissions { get; }
    DbSet<SubmissionWindow> SubmissionWindows { get; }
    DbSet<SubmissionApproval> SubmissionApprovals { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
