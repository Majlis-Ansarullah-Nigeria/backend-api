using ManagementApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApi.Infrastructure.Persistence.Configurations;

public class MemberPositionConfiguration : IEntityTypeConfiguration<MemberPosition>
{
    public void Configure(EntityTypeBuilder<MemberPosition> builder)
    {
        builder.ToTable("MemberPositions", "Membership");

        builder.HasKey(mp => mp.Id);

        builder.Property(mp => mp.PositionTitle)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(mp => mp.Responsibilities)
            .HasMaxLength(1000);

        builder.Property(mp => mp.OrganizationLevel)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(mp => mp.StartDate)
            .IsRequired();

        builder.Property(mp => mp.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Relationship with Member
        builder.HasOne(mp => mp.Member)
            .WithMany()
            .HasForeignKey(mp => mp.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for common queries
        builder.HasIndex(mp => mp.MemberId);
        builder.HasIndex(mp => mp.IsActive);
        builder.HasIndex(mp => new { mp.MemberId, mp.IsActive });
    }
}
