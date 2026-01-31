using ManagementApi.Domain.Entities.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ManagementApi.Infrastructure.Persistence.Configurations;

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences", "Reports");

        builder.HasKey(np => np.Id);

        builder.Property(np => np.UserId)
            .IsRequired();

        builder.Property(np => np.NotificationType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(np => np.IsInAppEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(np => np.IsEmailEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(np => np.IsPushEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        // Unique constraint: One preference per user per notification type
        builder.HasIndex(np => new { np.UserId, np.NotificationType })
            .IsUnique()
            .HasDatabaseName("IX_NotificationPreferences_UserId_Type");

        // Index for efficient querying
        builder.HasIndex(np => np.UserId)
            .HasDatabaseName("IX_NotificationPreferences_UserId");
    }
}
