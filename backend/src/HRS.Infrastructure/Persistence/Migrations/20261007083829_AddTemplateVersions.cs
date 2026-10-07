using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Prefer 'static readonly' fields over constant array arguments — migration code, runs once ever

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTemplateVersions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "template_versions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                report_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                version_number = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                schema_json = table.Column<string>(type: "jsonb", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_template_versions", x => x.id);
                table.ForeignKey(
                    name: "fk_template_versions_report_types_report_type_id",
                    column: x => x.report_type_id,
                    principalTable: "report_types",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_template_versions_report_type_id",
            table: "template_versions",
            column: "report_type_id");

        migrationBuilder.CreateIndex(
            name: "ix_template_versions_report_type_id_version_number",
            table: "template_versions",
            columns: new[] { "report_type_id", "version_number" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "template_versions");
    }
}
