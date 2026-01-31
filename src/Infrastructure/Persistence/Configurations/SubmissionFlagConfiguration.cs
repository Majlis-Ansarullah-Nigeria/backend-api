using ManagementApi.Domain.Entities.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApi.Infrastructure.Persistence.Configurations;

public class SubmissionFlagConfiguration : IEntityTypeConfiguration<SubmissionFlag>
{
    public void Configure(EntityTypeBuilder<SubmissionFlag> builder)
    {
        builder.ToTable("SubmissionFlags", "Reports");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.ReportSubmissionId)
            .IsRequired();

        builder.Property(f => f.FlaggerId)
            .IsRequired();

        builder.Property(f => f.FlaggerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(f => f.Reason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(f => f.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(f => f.FlaggedDate)
            .IsRequired();

        builder.Property(f => f.ResolvedByName)
            .HasMaxLength(200);

        builder.Property(f => f.ResolutionNotes)
            .HasMaxLength(500);

        // Relationships
        builder.HasOne(f => f.ReportSubmission)
            .WithMany()
            .HasForeignKey(f => f.ReportSubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for efficient querying
        builder.HasIndex(f => f.ReportSubmissionId)
            .HasDatabaseName("IX_SubmissionFlags_ReportSubmissionId");

        builder.HasIndex(f => f.IsActive)
            .HasDatabaseName("IX_SubmissionFlags_IsActive");

        builder.HasIndex(f => new { f.ReportSubmissionId, f.IsActive })
            .HasDatabaseName("IX_SubmissionFlags_ReportSubmissionId_IsActive");

        builder.HasIndex(f => f.FlaggerId)
            .HasDatabaseName("IX_SubmissionFlags_FlaggerId");

        builder.HasIndex(f => f.FlaggedDate)
            .HasDatabaseName("IX_SubmissionFlags_FlaggedDate");
    }
}
