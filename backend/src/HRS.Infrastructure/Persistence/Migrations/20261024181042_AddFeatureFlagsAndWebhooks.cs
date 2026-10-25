using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddFeatureFlagsAndWebhooks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "feature_flags",
            columns: table => new
            {
                module = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                enabled = table.Column<bool>(type: "boolean", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_feature_flags", x => x.module);
            });

        migrationBuilder.CreateTable(
            name: "webhook_endpoints",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                secret = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                events = table.Column<int[]>(type: "integer[]", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_webhook_endpoints", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "webhook_deliveries",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                endpoint_id = table.Column<Guid>(type: "uuid", nullable: false),
                @event = table.Column<int>(name: "event", type: "integer", nullable: false),
                payload_json = table.Column<string>(type: "jsonb", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                attempts = table.Column<int>(type: "integer", nullable: false),
                next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_status_code = table.Column<int>(type: "integer", nullable: true),
                last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_webhook_deliveries", x => x.id);
                table.ForeignKey(
                    name: "fk_webhook_deliveries_webhook_endpoints_endpoint_id",
                    column: x => x.endpoint_id,
                    principalTable: "webhook_endpoints",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_webhook_deliveries_endpoint_id_created_at",
            table: "webhook_deliveries",
            columns: new[] { "endpoint_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_webhook_deliveries_status_next_attempt_at",
            table: "webhook_deliveries",
            columns: new[] { "status", "next_attempt_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "feature_flags");

        migrationBuilder.DropTable(
            name: "webhook_deliveries");

        migrationBuilder.DropTable(
            name: "webhook_endpoints");
    }
}
