using ManagementApi.Domain.Entities.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApi.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", "Reports");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(n => n.RecipientId)
            .IsRequired();

        builder.Property(n => n.RecipientName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(n => n.RelatedEntityId);

        builder.Property(n => n.RelatedEntityType)
            .HasMaxLength(100);

        builder.Property(n => n.IsRead)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(n => n.ReadOn);

        builder.Property(n => n.Priority)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(NotificationPriority.Normal);

        // Indexes for efficient querying
        builder.HasIndex(n => n.RecipientId)
            .HasDatabaseName("IX_Notifications_RecipientId");

        builder.HasIndex(n => new { n.RecipientId, n.IsRead })
            .HasDatabaseName("IX_Notifications_RecipientId_IsRead");

        builder.HasIndex(n => n.CreatedOn)
            .HasDatabaseName("IX_Notifications_CreatedOn");

        builder.HasIndex(n => new { n.RelatedEntityId, n.RelatedEntityType })
            .HasDatabaseName("IX_Notifications_RelatedEntity");
    }
}
