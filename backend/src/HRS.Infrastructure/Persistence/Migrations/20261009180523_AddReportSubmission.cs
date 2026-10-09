using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddReportSubmission : Migration
{
    private static readonly string[] IdempotencyRecordsUserIdKeyColumns = ["user_id", "key"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "reference_number",
            table: "report_instances",
            type: "text",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "idempotency_records",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "text", nullable: false),
                response_json = table.Column<string>(type: "jsonb", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_idempotency_records", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_report_instances_reference_number",
            table: "report_instances",
            column: "reference_number",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_idempotency_records_user_id_key",
            table: "idempotency_records",
            columns: IdempotencyRecordsUserIdKeyColumns,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "idempotency_records");

        migrationBuilder.DropIndex(
            name: "ix_report_instances_reference_number",
            table: "report_instances");

        migrationBuilder.DropColumn(
            name: "reference_number",
            table: "report_instances");
    }
}
