using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOrganizationSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "organization_settings",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                logo_storage_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                logo_content_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                brand_color = table.Column<int>(type: "integer", nullable: false),
                time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                date_format = table.Column<int>(type: "integer", nullable: false),
                fiscal_year_start_month = table.Column<int>(type: "integer", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_organization_settings", x => x.id);
            });

        // FR-ADM-001's single settings row, with the defaults the app used before settings existed.
        migrationBuilder.Sql("""
            INSERT INTO organization_settings (id, name, brand_color, time_zone, date_format, fiscal_year_start_month)
            VALUES ('00000000-0000-0000-0000-0000000000f1', 'Hierarchical Reporting Service', 0, 'Africa/Lagos', 0, 1)
            ON CONFLICT (id) DO NOTHING;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "organization_settings");
    }
}
