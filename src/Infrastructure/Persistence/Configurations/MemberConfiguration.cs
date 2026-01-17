using ManagementApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApi.Infrastructure.Persistence.Configurations;

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("Members", "Membership");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.ChandaNo)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(m => m.ChandaNo)
            .IsUnique();

        builder.Property(m => m.Surname)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.FirstName)
            .HasMaxLength(100);

        builder.Property(m => m.MiddleName)
            .HasMaxLength(100);

        builder.Property(m => m.Email)
            .HasMaxLength(256);

        builder.Property(m => m.PhoneNo)
            .HasMaxLength(100);

        builder.Property(m => m.Address)
            .HasMaxLength(500);

        builder.Property(m => m.PhotoUrl)
            .HasMaxLength(500);

        builder.Property(m => m.Signature)
            .HasMaxLength(500);

        builder.HasOne(m => m.Muqam)
            .WithMany(mu => mu.Members)
            .HasForeignKey(m => m.MuqamId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
