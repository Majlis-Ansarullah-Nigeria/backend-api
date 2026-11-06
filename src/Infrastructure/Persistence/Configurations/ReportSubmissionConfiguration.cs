using ManagementApi.Domain.Entities.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApi.Infrastructure.Persistence.Configurations;

public class ReportSubmissionConfiguration : IEntityTypeConfiguration<ReportSubmission>
{
    public void Configure(EntityTypeBuilder<ReportSubmission> builder)
    {
        builder.Property(s => s.ReturnReason)
            .HasMaxLength(1000);

        // One report per unit per window. Reports belong to the unit, not the person.
        builder.HasIndex(s => new { s.SubmissionWindowId, s.OrganizationLevel, s.UnitId })
            .IsUnique()
            .HasFilter("\"SubmissionWindowId\" IS NOT NULL")
            .HasDatabaseName("IX_ReportSubmissions_Window_Unit");

        builder.HasIndex(s => new { s.Status, s.SubmittedAt });
        builder.HasIndex(s => s.ZoneId);
        builder.HasIndex(s => s.DilaId);
    }
}

public class SubmissionApprovalConfiguration : IEntityTypeConfiguration<SubmissionApproval>
{
    public void Configure(EntityTypeBuilder<SubmissionApproval> builder)
    {
        // Ids are assigned in the domain. Without this, a review action added to an already
        // saved report is treated as an existing row and the save fails.
        builder.Property(a => a.Id).ValueGeneratedNever();
    }
}

public class WindowExtensionConfiguration : IEntityTypeConfiguration<WindowExtension>
{
    public void Configure(EntityTypeBuilder<WindowExtension> builder)
    {
        builder.ToTable("WindowExtensions");
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();

        builder.HasOne<SubmissionWindow>()
            .WithMany(w => w.Extensions)
            .HasForeignKey(e => e.SubmissionWindowId)
            .OnDelete(DeleteBehavior.Cascade);

        // One extension per unit per window
        builder.HasIndex(e => new { e.SubmissionWindowId, e.OrganizationLevel, e.UnitId }).IsUnique();
    }
}
