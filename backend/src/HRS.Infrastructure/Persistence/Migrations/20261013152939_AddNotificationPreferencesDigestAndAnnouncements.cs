using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddNotificationPreferencesDigestAndAnnouncements : Migration
{
    private static readonly string[] NotificationsUserIdIsBannerColumns = ["user_id", "is_banner"];
    private static readonly string[] NotificationPreferencesUserIdCategoryColumns = ["user_id", "category"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_banner",
            table: "notifications",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "announcements",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "text", nullable: false),
                message = table.Column<string>(type: "text", nullable: false),
                audience = table.Column<int>(type: "integer", nullable: false),
                show_as_banner = table.Column<bool>(type: "boolean", nullable: false),
                send_email = table.Column<bool>(type: "boolean", nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                delivered_count = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_announcements", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "notification_preferences",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                category = table.Column<int>(type: "integer", nullable: false),
                in_app_enabled = table.Column<bool>(type: "boolean", nullable: false),
                email_enabled = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_notification_preferences", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "user_notification_settings",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                digest_enabled = table.Column<bool>(type: "boolean", nullable: false),
                last_digest_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_notification_settings", x => x.user_id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_notifications_user_id_is_banner",
            table: "notifications",
            columns: NotificationsUserIdIsBannerColumns);

        migrationBuilder.CreateIndex(
            name: "ix_announcements_created_at",
            table: "announcements",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "ix_notification_preferences_user_id_category",
            table: "notification_preferences",
            columns: NotificationPreferencesUserIdCategoryColumns,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "announcements");

        migrationBuilder.DropTable(
            name: "notification_preferences");

        migrationBuilder.DropTable(
            name: "user_notification_settings");

        migrationBuilder.DropIndex(
            name: "ix_notifications_user_id_is_banner",
            table: "notifications");

        migrationBuilder.DropColumn(
            name: "is_banner",
            table: "notifications");
    }
}
