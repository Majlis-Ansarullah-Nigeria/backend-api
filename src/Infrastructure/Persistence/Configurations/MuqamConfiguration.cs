using ManagementApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApi.Infrastructure.Persistence.Configurations;

public class MuqamConfiguration : IEntityTypeConfiguration<Muqam>
{
    public void Configure(EntityTypeBuilder<Muqam> builder)
    {
        builder.ToTable("Muqams", "Organization");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Code)
            .HasMaxLength(50);

        builder.Property(m => m.Address)
            .HasMaxLength(500);

        builder.Property(m => m.ContactPerson)
            .HasMaxLength(200);

        builder.Property(m => m.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(m => m.Email)
            .HasMaxLength(256);

        builder.HasOne(m => m.Dila)
            .WithMany(d => d.Muqams)
            .HasForeignKey(m => m.DilaId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
