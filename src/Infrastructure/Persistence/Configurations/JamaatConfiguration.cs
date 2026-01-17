using ManagementApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApi.Infrastructure.Persistence.Configurations;

public class JamaatConfiguration : IEntityTypeConfiguration<Jamaat>
{
    public void Configure(EntityTypeBuilder<Jamaat> builder)
    {
        builder.ToTable("Jamaats", "Organization");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.JamaatId)
            .IsRequired();

        builder.HasIndex(j => j.JamaatId)
            .IsUnique();

        builder.Property(j => j.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(j => j.Code)
            .HasMaxLength(50);

        builder.HasOne(j => j.Muqam)
            .WithMany(m => m.Jamaats)
            .HasForeignKey(j => j.MuqamId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure to ignore domain events collection for EF
        builder.Ignore(j => j.DomainEvents);
    }
}
