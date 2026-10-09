using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddReportAttachments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "report_attachments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                report_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                field_key = table.Column<string>(type: "text", nullable: false),
                file_name = table.Column<string>(type: "text", nullable: false),
                content_type = table.Column<string>(type: "text", nullable: false),
                size_bytes = table.Column<long>(type: "bigint", nullable: false),
                storage_key = table.Column<string>(type: "text", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_report_attachments", x => x.id);
                table.ForeignKey(
                    name: "fk_report_attachments_report_instances_report_instance_id",
                    column: x => x.report_instance_id,
                    principalTable: "report_instances",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_report_attachments_report_instance_id",
            table: "report_attachments",
            column: "report_instance_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "report_attachments");
    }
}
