using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddReportCommentsAndAssignment : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "assigned_to_user_id",
            table: "report_instances",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "report_comments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                report_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                field_key = table.Column<string>(type: "text", nullable: false),
                parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                body = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_report_comments", x => x.id);
                table.ForeignKey(
                    name: "fk_report_comments_report_comments_parent_id",
                    column: x => x.parent_id,
                    principalTable: "report_comments",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_report_comments_report_instances_report_instance_id",
                    column: x => x.report_instance_id,
                    principalTable: "report_instances",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_report_comments_parent_id",
            table: "report_comments",
            column: "parent_id");

        migrationBuilder.CreateIndex(
            name: "ix_report_comments_report_instance_id",
            table: "report_comments",
            column: "report_instance_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "report_comments");

        migrationBuilder.DropColumn(
            name: "assigned_to_user_id",
            table: "report_instances");
    }
}
