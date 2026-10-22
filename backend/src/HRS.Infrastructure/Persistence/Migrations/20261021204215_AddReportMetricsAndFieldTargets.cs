using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddReportMetricsAndFieldTargets : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "report_field_targets",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                report_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                field_key = table.Column<string>(type: "text", nullable: false),
                level = table.Column<int>(type: "integer", nullable: false),
                value = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_report_field_targets", x => x.id);
                table.ForeignKey(
                    name: "fk_report_field_targets_report_types_report_type_id",
                    column: x => x.report_type_id,
                    principalTable: "report_types",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "report_metrics",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                report_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                window_id = table.Column<Guid>(type: "uuid", nullable: false),
                unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                unit_path = table.Column<string>(type: "text", nullable: false),
                field_key = table.Column<string>(type: "text", nullable: false),
                value = table.Column<decimal>(type: "numeric(20,4)", precision: 20, scale: 4, nullable: false),
                period_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_report_metrics", x => x.id);
                table.ForeignKey(
                    name: "fk_report_metrics_report_instances_report_instance_id",
                    column: x => x.report_instance_id,
                    principalTable: "report_instances",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_report_field_targets_report_type_id_field_key_level",
            table: "report_field_targets",
            columns: new[] { "report_type_id", "field_key", "level" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_report_metrics_field_key_window_id",
            table: "report_metrics",
            columns: new[] { "field_key", "window_id" });

        migrationBuilder.CreateIndex(
            name: "ix_report_metrics_report_instance_id_field_key",
            table: "report_metrics",
            columns: new[] { "report_instance_id", "field_key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_report_metrics_unit_path",
            table: "report_metrics",
            column: "unit_path")
            .Annotation("Npgsql:IndexOperators", new[] { "text_pattern_ops" });

        // FR-DSH-002 backfill: copy the figures (Number=2, Currency=3, Percentage=4 in the
        // template schema) of every already-Acknowledged (5) report, as ReportMetricWriter
        // does on acknowledge from now on. Numbers saved as numeric strings count too.
        // Idempotent.
        migrationBuilder.Sql("""
            INSERT INTO report_metrics (id, report_instance_id, window_id, unit_id, unit_path, field_key, value, period_start)
            SELECT gen_random_uuid(), i.id, i.window_id, i.unit_id, u.path, kv.key, (kv.value #>> '{}')::numeric, w.opens_at
            FROM report_instances i
            JOIN units u ON u.id = i.unit_id
            JOIN submission_windows w ON w.id = i.window_id
            JOIN template_versions tv ON tv.id = i.template_version_id
            CROSS JOIN LATERAL jsonb_each(i.answers_json) kv
            WHERE i.status = 5
              AND EXISTS (
                  SELECT 1
                  FROM jsonb_array_elements(tv.schema_json -> 'sections') s
                  CROSS JOIN LATERAL jsonb_array_elements(s -> 'fields') f
                  WHERE f ->> 'key' = kv.key AND f ->> 'type' IN ('2', '3', '4'))
              AND (jsonb_typeof(kv.value) = 'number'
                   OR (jsonb_typeof(kv.value) = 'string' AND btrim(kv.value #>> '{}') ~ '^-?[0-9]+(\.[0-9]+)?$'))
              AND NOT EXISTS (
                  SELECT 1 FROM report_metrics m WHERE m.report_instance_id = i.id AND m.field_key = kv.key);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "report_field_targets");

        migrationBuilder.DropTable(
            name: "report_metrics");
    }
}
