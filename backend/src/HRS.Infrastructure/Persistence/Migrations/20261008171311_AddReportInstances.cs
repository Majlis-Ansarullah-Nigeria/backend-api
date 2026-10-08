using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddReportInstances : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "report_instances",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                window_id = table.Column<Guid>(type: "uuid", nullable: false),
                unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                template_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                answers_json = table.Column<string>(type: "jsonb", nullable: false),
                is_late = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                returned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                return_comment = table.Column<string>(type: "text", nullable: true),
                revision_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                reopened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_report_instances", x => x.id);
                table.ForeignKey(
                    name: "fk_report_instances_submission_windows_window_id",
                    column: x => x.window_id,
                    principalTable: "submission_windows",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_report_instances_template_versions_template_version_id",
                    column: x => x.template_version_id,
                    principalTable: "template_versions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_report_instances_units_unit_id",
                    column: x => x.unit_id,
                    principalTable: "units",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_report_instances_template_version_id",
            table: "report_instances",
            column: "template_version_id");

        migrationBuilder.CreateIndex(
            name: "ix_report_instances_unit_id",
            table: "report_instances",
            column: "unit_id");

        migrationBuilder.CreateIndex(
            name: "ix_report_instances_window_id_unit_id",
            table: "report_instances",
            columns: new[] { "window_id", "unit_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "report_instances");
    }
}
