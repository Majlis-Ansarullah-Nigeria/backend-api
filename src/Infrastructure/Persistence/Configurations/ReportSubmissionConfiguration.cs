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

        builder.Property(s => s.ReferenceNumber).HasMaxLength(80);
        builder.HasIndex(s => s.ReferenceNumber)
            .IsUnique()
            .HasFilter("\"ReferenceNumber\" IS NOT NULL");

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

public class ReportTypeConfiguration : IEntityTypeConfiguration<ReportType>
{
    public void Configure(EntityTypeBuilder<ReportType> builder)
    {
        builder.ToTable("ReportTypes");
        builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
        builder.Property(t => t.Code).HasMaxLength(6).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.HasIndex(t => t.Code).IsUnique();
    }
}

public class ReportTemplateReportTypeConfiguration : IEntityTypeConfiguration<ReportTemplate>
{
    public void Configure(EntityTypeBuilder<ReportTemplate> builder)
    {
        builder.HasOne(t => t.ReportTypeEntity)
            .WithMany()
            .HasForeignKey(t => t.ReportTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ReportReminderConfiguration : IEntityTypeConfiguration<ReportReminder>
{
    public void Configure(EntityTypeBuilder<ReportReminder> builder)
    {
        builder.ToTable("ReportReminders");
        builder.Property(r => r.Id).ValueGeneratedNever();

        // One reminder of each kind per unit per window
        builder.HasIndex(r => new { r.SubmissionWindowId, r.OrganizationLevel, r.UnitId, r.DaysBeforeDue }).IsUnique();
    }
}

public class EmailChangeRequestConfiguration : IEntityTypeConfiguration<ManagementApi.Domain.Identity.EmailChangeRequest>
{
    public void Configure(EntityTypeBuilder<ManagementApi.Domain.Identity.EmailChangeRequest> builder)
    {
        builder.ToTable("EmailChangeRequests", "Identity");
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.ChandaNo).HasMaxLength(50).IsRequired();
        builder.Property(e => e.CurrentEmail).HasMaxLength(256);
        builder.Property(e => e.RequestedEmail).HasMaxLength(256).IsRequired();
        builder.Property(e => e.PhoneNumber).HasMaxLength(30);
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.Property(e => e.RejectionReason).HasMaxLength(500);
        builder.HasIndex(e => new { e.Status, e.RequestedAt });
        builder.HasIndex(e => e.UserId);
    }
}
