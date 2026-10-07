using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1861 // Prefer 'static readonly' fields over constant array arguments — migration code, runs once ever

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSubmissionWindows : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "submission_windows",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                report_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                period_label = table.Column<string>(type: "text", nullable: false),
                opens_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                grace_days = table.Column<int>(type: "integer", nullable: false),
                closes_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_submission_windows", x => x.id);
                table.ForeignKey(
                    name: "fk_submission_windows_report_types_report_type_id",
                    column: x => x.report_type_id,
                    principalTable: "report_types",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "submission_window_due_dates",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                level = table.Column<int>(type: "integer", nullable: false),
                due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                submission_window_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_submission_window_due_dates", x => x.id);
                table.ForeignKey(
                    name: "fk_submission_window_due_dates_submission_windows_submission_w",
                    column: x => x.submission_window_id,
                    principalTable: "submission_windows",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_submission_window_due_dates_submission_window_id_level",
            table: "submission_window_due_dates",
            columns: new[] { "submission_window_id", "level" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_submission_windows_report_type_id",
            table: "submission_windows",
            column: "report_type_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "submission_window_due_dates");

        migrationBuilder.DropTable(
            name: "submission_windows");
    }
}
