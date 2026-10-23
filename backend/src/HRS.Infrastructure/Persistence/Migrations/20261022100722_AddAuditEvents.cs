using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAuditEvents : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "audit_events",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                actor_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                entity_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                entity_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                before_json = table.Column<string>(type: "jsonb", nullable: true),
                after_json = table.Column<string>(type: "jsonb", nullable: true),
                ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_audit_events", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_audit_events_action_occurred_at",
            table: "audit_events",
            columns: new[] { "action", "occurred_at" });

        migrationBuilder.CreateIndex(
            name: "ix_audit_events_actor_user_id_occurred_at",
            table: "audit_events",
            columns: new[] { "actor_user_id", "occurred_at" });

        migrationBuilder.CreateIndex(
            name: "ix_audit_events_occurred_at",
            table: "audit_events",
            column: "occurred_at");

        migrationBuilder.CreateIndex(
            name: "ix_audit_events_unit_id_occurred_at",
            table: "audit_events",
            columns: new[] { "unit_id", "occurred_at" });

        // FR-AUD-001: append-only. The application never updates or deletes an entry; the
        // database refuses to as well, whoever asks.
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION audit_events_append_only() RETURNS trigger AS $$
            BEGIN
                RAISE EXCEPTION 'audit_events is append-only (% refused)', TG_OP;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER audit_events_append_only
            BEFORE UPDATE OR DELETE OR TRUNCATE ON audit_events
            FOR EACH STATEMENT EXECUTE FUNCTION audit_events_append_only();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS audit_events_append_only ON audit_events;
            DROP FUNCTION IF EXISTS audit_events_append_only();
            """);

        migrationBuilder.DropTable(
            name: "audit_events");
    }
}
