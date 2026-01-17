using ManagementApi.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApi.Infrastructure.Persistence.Configurations;

public class ApplicationRolePermissionConfiguration : IEntityTypeConfiguration<ApplicationRolePermission>
{
    public void Configure(EntityTypeBuilder<ApplicationRolePermission> builder)
    {
        builder.ToTable("RolePermissions", "Identity");

        builder.HasKey(rp => rp.Id);

        builder.Property(rp => rp.Permission)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(rp => new { rp.RoleId, rp.Permission })
            .IsUnique();

        builder.HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
