using ManagementApi.Domain.Entities.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApi.Infrastructure.Persistence.Configurations;

public class FileAttachmentConfiguration : IEntityTypeConfiguration<FileAttachment>
{
    public void Configure(EntityTypeBuilder<FileAttachment> builder)
    {
        builder.ToTable("FileAttachments", "Reports");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(f => f.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.FileSize)
            .IsRequired();

        builder.Property(f => f.FileData)
            .IsRequired()
            .HasColumnType("varbinary(max)"); // Store as binary data in SQL Server

        builder.Property(f => f.Description)
            .HasMaxLength(500);

        builder.HasOne(f => f.ReportSubmission)
            .WithMany(s => s.FileAttachments)
            .HasForeignKey(f => f.ReportSubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.ReportSubmissionId);
        builder.HasIndex(f => f.QuestionId);
    }
}
