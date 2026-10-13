using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddNotificationCentreAndPushSubscriptions : Migration
{
    private static readonly string[] NotificationsUserIdCreatedAtColumns = ["user_id", "created_at"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<int>(type: "integer", nullable: false),
                subject = table.Column<string>(type: "text", nullable: false),
                body = table.Column<string>(type: "text", nullable: false),
                link = table.Column<string>(type: "text", nullable: true),
                outbox_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_notifications", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "push_subscriptions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                endpoint = table.Column<string>(type: "text", nullable: false),
                p256dh = table.Column<string>(type: "text", nullable: false),
                auth = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_push_subscriptions", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_notifications_outbox_message_id",
            table: "notifications",
            column: "outbox_message_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_notifications_user_id_created_at",
            table: "notifications",
            columns: NotificationsUserIdCreatedAtColumns);

        migrationBuilder.CreateIndex(
            name: "ix_push_subscriptions_endpoint",
            table: "push_subscriptions",
            column: "endpoint",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_push_subscriptions_user_id",
            table: "push_subscriptions",
            column: "user_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "notifications");

        migrationBuilder.DropTable(
            name: "push_subscriptions");
    }
}
