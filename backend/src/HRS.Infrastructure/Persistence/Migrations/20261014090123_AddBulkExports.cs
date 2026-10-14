using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddBulkExports : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "bulk_exports",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                format = table.Column<int>(type: "integer", nullable: false),
                scope_kind = table.Column<int>(type: "integer", nullable: false),
                scope_json = table.Column<string>(type: "jsonb", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                storage_key = table.Column<string>(type: "text", nullable: true),
                report_count = table.Column<int>(type: "integer", nullable: false),
                error_message = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_bulk_exports", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_bulk_exports_created_at",
            table: "bulk_exports",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "ix_bulk_exports_requested_by_user_id",
            table: "bulk_exports",
            column: "requested_by_user_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "bulk_exports");
    }
}
