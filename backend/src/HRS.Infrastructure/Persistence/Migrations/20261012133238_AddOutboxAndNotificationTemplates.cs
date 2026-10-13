using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOutboxAndNotificationTemplates : Migration
{
    private static readonly string[] OutboxMessagesProcessedAtCreatedAtColumns = ["processed_at", "created_at"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "notification_templates",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<int>(type: "integer", nullable: false),
                subject = table.Column<string>(type: "text", nullable: false),
                body = table.Column<string>(type: "text", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_notification_templates", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<int>(type: "integer", nullable: false),
                recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                payload_json = table.Column<string>(type: "jsonb", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                attempts = table.Column<int>(type: "integer", nullable: false),
                last_error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_outbox_messages", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_notification_templates_event_type",
            table: "notification_templates",
            column: "event_type",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_outbox_messages_processed_at_created_at",
            table: "outbox_messages",
            columns: OutboxMessagesProcessedAtCreatedAtColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "notification_templates");

        migrationBuilder.DropTable(
            name: "outbox_messages");
    }
}
