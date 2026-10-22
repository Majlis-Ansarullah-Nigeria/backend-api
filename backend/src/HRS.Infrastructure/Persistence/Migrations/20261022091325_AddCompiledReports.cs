using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCompiledReports : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "compiled_reports",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                window_id = table.Column<Guid>(type: "uuid", nullable: false),
                narrative = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                narrative_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                narrative_updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_compiled_reports", x => x.id);
                table.ForeignKey(
                    name: "fk_compiled_reports_submission_windows_window_id",
                    column: x => x.window_id,
                    principalTable: "submission_windows",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_compiled_reports_window_id",
            table: "compiled_reports",
            column: "window_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "compiled_reports");
    }
}
