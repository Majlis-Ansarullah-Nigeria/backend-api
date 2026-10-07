using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable
#pragma warning disable CA1861

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWindowExtensionsAndReopens : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "window_extensions",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                new_due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                new_closes_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                reason = table.Column<string>(type: "text", nullable: false),
                granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                submission_window_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_window_extensions", x => x.id);
                table.ForeignKey(
                    name: "fk_window_extensions_submission_windows_submission_window_id",
                    column: x => x.submission_window_id,
                    principalTable: "submission_windows",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_window_extensions_units_unit_id",
                    column: x => x.unit_id,
                    principalTable: "units",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "window_reopens",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                reopened_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                reason = table.Column<string>(type: "text", nullable: false),
                reopened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                submission_window_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_window_reopens", x => x.id);
                table.ForeignKey(
                    name: "fk_window_reopens_submission_windows_submission_window_id",
                    column: x => x.submission_window_id,
                    principalTable: "submission_windows",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_window_reopens_units_unit_id",
                    column: x => x.unit_id,
                    principalTable: "units",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_window_extensions_submission_window_id_unit_id",
            table: "window_extensions",
            columns: new[] { "submission_window_id", "unit_id" });

        migrationBuilder.CreateIndex(
            name: "ix_window_extensions_unit_id",
            table: "window_extensions",
            column: "unit_id");

        migrationBuilder.CreateIndex(
            name: "ix_window_reopens_submission_window_id_unit_id",
            table: "window_reopens",
            columns: new[] { "submission_window_id", "unit_id" });

        migrationBuilder.CreateIndex(
            name: "ix_window_reopens_unit_id",
            table: "window_reopens",
            column: "unit_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "window_extensions");

        migrationBuilder.DropTable(
            name: "window_reopens");
    }
}
