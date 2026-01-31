using ManagementApi.Domain.Entities.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApi.Infrastructure.Persistence.Configurations;

public class SubmissionCommentConfiguration : IEntityTypeConfiguration<SubmissionComment>
{
    public void Configure(EntityTypeBuilder<SubmissionComment> builder)
    {
        builder.ToTable("SubmissionComments", "Reports");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ReportSubmissionId)
            .IsRequired();

        builder.Property(c => c.CommenterId)
            .IsRequired();

        builder.Property(c => c.CommenterName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Content)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(c => c.IsEdited)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Relationships
        builder.HasOne(c => c.ReportSubmission)
            .WithMany()
            .HasForeignKey(c => c.ReportSubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Self-referencing relationship for threading (replies)
        builder.HasOne(c => c.ParentComment)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete of replies when parent is deleted

        // Indexes for efficient querying
        builder.HasIndex(c => c.ReportSubmissionId)
            .HasDatabaseName("IX_SubmissionComments_ReportSubmissionId");

        builder.HasIndex(c => c.ParentCommentId)
            .HasDatabaseName("IX_SubmissionComments_ParentCommentId");

        builder.HasIndex(c => new { c.ReportSubmissionId, c.IsDeleted })
            .HasDatabaseName("IX_SubmissionComments_ReportSubmissionId_IsDeleted");

        builder.HasIndex(c => c.CommenterId)
            .HasDatabaseName("IX_SubmissionComments_CommenterId");

        builder.HasIndex(c => c.CreatedOn)
            .HasDatabaseName("IX_SubmissionComments_CreatedOn");

        builder.HasIndex(c => new { c.ParentCommentId, c.CreatedOn })
            .HasDatabaseName("IX_SubmissionComments_ParentCommentId_CreatedOn");
    }
}
