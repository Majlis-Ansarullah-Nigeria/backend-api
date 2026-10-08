using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWindowJobRuns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "window_job_runs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                window_id = table.Column<Guid>(type: "uuid", nullable: false),
                unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<int>(type: "integer", nullable: false),
                event_key = table.Column<string>(type: "text", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_window_job_runs", x => x.id);
                table.ForeignKey(
                    name: "fk_window_job_runs_submission_windows_window_id",
                    column: x => x.window_id,
                    principalTable: "submission_windows",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_window_job_runs_units_unit_id",
                    column: x => x.unit_id,
                    principalTable: "units",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_window_job_runs_unit_id",
            table: "window_job_runs",
            column: "unit_id");

        migrationBuilder.CreateIndex(
            name: "ix_window_job_runs_window_id_unit_id_event_type_event_key",
            table: "window_job_runs",
            columns: new[] { "window_id", "unit_id", "event_type", "event_key" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "window_job_runs");
    }
}
