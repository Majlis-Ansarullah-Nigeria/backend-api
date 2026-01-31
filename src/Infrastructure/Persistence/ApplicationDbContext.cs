using System.Reflection;
using ManagementApi.Application.Common.Interfaces;
using ManagementApi.Domain.Common;
using ManagementApi.Domain.Entities;
using ManagementApi.Domain.Entities.Reports;
using ManagementApi.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ManagementApi.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext
{
    private readonly ICurrentUser? _currentUser;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUser currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberPosition> MemberPositions => Set<MemberPosition>();
    public DbSet<Muqam> Muqams => Set<Muqam>();
    public DbSet<Dila> Dilas => Set<Dila>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Jamaat> Jamaats => Set<Jamaat>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<ApplicationRole> Roles => Set<ApplicationRole>();
    public DbSet<ApplicationRolePermission> RolePermissions => Set<ApplicationRolePermission>();

    // Reports
    public DbSet<ReportTemplate> ReportTemplates => Set<ReportTemplate>();
    public DbSet<ReportSection> ReportSections => Set<ReportSection>();
    public DbSet<ReportQuestion> ReportQuestions => Set<ReportQuestion>();
    public DbSet<ReportSubmission> ReportSubmissions => Set<ReportSubmission>();
    public DbSet<SubmissionWindow> SubmissionWindows => Set<SubmissionWindow>();
    public DbSet<SubmissionApproval> SubmissionApprovals => Set<SubmissionApproval>();
    public DbSet<FileAttachment> FileAttachments => Set<FileAttachment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<SubmissionFlag> SubmissionFlags => Set<SubmissionFlag>();
    public DbSet<SubmissionComment> SubmissionComments => Set<SubmissionComment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Ignore abstract base classes that shouldn't be mapped to tables
        builder.Ignore<DomainEvent>();

        // Apply all entity configurations from the current assembly
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Configure Identity table names
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users", "Identity");
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable("Roles", "Identity");
        });

        builder.Entity<IdentityUserRole<Guid>>(entity =>
        {
            entity.ToTable("UserRoles", "Identity");
        });

        builder.Entity<IdentityUserClaim<Guid>>(entity =>
        {
            entity.ToTable("UserClaims", "Identity");
        });

        builder.Entity<IdentityUserLogin<Guid>>(entity =>
        {
            entity.ToTable("UserLogins", "Identity");
        });

        builder.Entity<IdentityRoleClaim<Guid>>(entity =>
        {
            entity.ToTable("RoleClaims", "Identity");
        });

        builder.Entity<IdentityUserToken<Guid>>(entity =>
        {
            entity.ToTable("UserTokens", "Identity");
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        HandleAuditableEntities();
        HandleDomainEvents();

        return await base.SaveChangesAsync(cancellationToken);
    }

    private void HandleAuditableEntities()
    {
        var userId = _currentUser?.GetUserId();

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedOn = DateTime.UtcNow;
                    entry.Entity.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.LastModifiedOn = DateTime.UtcNow;
                    entry.Entity.LastModifiedBy = userId;
                    break;
            }
        }
    }

    private void HandleDomainEvents()
    {
        var domainEntities = ChangeTracker
            .Entries<BaseEntity>()
            .Where(x => x.Entity.DomainEvents.Any())
            .ToList();

        foreach (var entry in domainEntities)
        {
            // Domain events would be published here via IMediator
            // For now, we just clear them
            entry.Entity.ClearDomainEvents();
        }
    }
}
