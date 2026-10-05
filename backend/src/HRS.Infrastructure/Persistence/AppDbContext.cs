using HRS.Application.Common.Interfaces;
using HRS.Domain.Access;
using HRS.Domain.Invitations;
using HRS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HRS.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options), IAppDbContext
{
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Identity's defaults are "AspNetUsers" etc.; rename to match this
        // project's plain snake_case table-naming convention.
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        builder.Entity<UserSession>(session =>
        {
            session.ToTable("user_sessions");
            session.HasKey(s => s.Id);
            session.Property(s => s.RefreshTokenHash).IsRequired();
            session.HasIndex(s => s.RefreshTokenHash).IsUnique();
            session.HasIndex(s => s.UserId);
            session
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Invitation>(invitation =>
        {
            invitation.ToTable("invitations");
            invitation.HasKey(i => i.Id);
            invitation.Property(i => i.Email).IsRequired();
            invitation.Property(i => i.Name).IsRequired();
            invitation.Property(i => i.TokenHash).IsRequired();
            invitation.HasIndex(i => i.TokenHash).IsUnique();
            invitation.HasIndex(i => i.Email);
        });

        builder.Entity<RoleAssignment>(assignment =>
        {
            assignment.ToTable("role_assignments");
            assignment.HasKey(a => a.Id);
            assignment.HasIndex(a => a.UserId);
            assignment.HasIndex(a => a.UnitId);
            assignment
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            // UnitId intentionally has no foreign key: Units don't exist until M2.1.
        });

        builder.Entity<ApplicationUser>(user =>
        {
            user
                .HasOne<RoleAssignment>()
                .WithMany()
                .HasForeignKey(u => u.ActiveRoleAssignmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
