using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1861 // Prefer 'static readonly' fields over constant array arguments — migration code, runs once ever

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWindowSchedules : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "schedule_id",
            table: "submission_windows",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid[]>(
            name: "target_unit_ids",
            table: "submission_windows",
            type: "uuid[]",
            nullable: false,
            defaultValue: Array.Empty<Guid>());

        migrationBuilder.CreateTable(
            name: "window_schedules",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                report_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                frequency = table.Column<int>(type: "integer", nullable: false),
                opens_day_offset = table.Column<int>(type: "integer", nullable: false),
                grace_days = table.Column<int>(type: "integer", nullable: false),
                closes_day_offset = table.Column<int>(type: "integer", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_window_schedules", x => x.id);
                table.ForeignKey(
                    name: "fk_window_schedules_report_types_report_type_id",
                    column: x => x.report_type_id,
                    principalTable: "report_types",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "window_schedule_due_offsets",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                level = table.Column<int>(type: "integer", nullable: false),
                due_day_offset = table.Column<int>(type: "integer", nullable: false),
                window_schedule_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_window_schedule_due_offsets", x => x.id);
                table.ForeignKey(
                    name: "fk_window_schedule_due_offsets_window_schedules_window_schedul",
                    column: x => x.window_schedule_id,
                    principalTable: "window_schedules",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_submission_windows_schedule_id",
            table: "submission_windows",
            column: "schedule_id");

        migrationBuilder.CreateIndex(
            name: "ix_window_schedule_due_offsets_window_schedule_id_level",
            table: "window_schedule_due_offsets",
            columns: new[] { "window_schedule_id", "level" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_window_schedules_report_type_id",
            table: "window_schedules",
            column: "report_type_id");

        migrationBuilder.AddForeignKey(
            name: "fk_submission_windows_window_schedules_schedule_id",
            table: "submission_windows",
            column: "schedule_id",
            principalTable: "window_schedules",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_submission_windows_window_schedules_schedule_id",
            table: "submission_windows");

        migrationBuilder.DropTable(
            name: "window_schedule_due_offsets");

        migrationBuilder.DropTable(
            name: "window_schedules");

        migrationBuilder.DropIndex(
            name: "ix_submission_windows_schedule_id",
            table: "submission_windows");

        migrationBuilder.DropColumn(
            name: "schedule_id",
            table: "submission_windows");

        migrationBuilder.DropColumn(
            name: "target_unit_ids",
            table: "submission_windows");
    }
}
