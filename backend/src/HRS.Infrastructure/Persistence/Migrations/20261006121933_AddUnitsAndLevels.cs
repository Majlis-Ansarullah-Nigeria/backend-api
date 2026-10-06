using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // Prefer 'static readonly' fields over constant array arguments — seed data, runs once ever

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddUnitsAndLevels : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "unit_levels",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<int>(type: "integer", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                plural_name = table.Column<string>(type: "text", nullable: false),
                depth = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_unit_levels", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "units",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "text", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                level_id = table.Column<Guid>(type: "uuid", nullable: false),
                parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                path = table.Column<string>(type: "text", nullable: false),
                address = table.Column<string>(type: "text", nullable: true),
                contact_phone = table.Column<string>(type: "text", nullable: true),
                contact_email = table.Column<string>(type: "text", nullable: true),
                latitude = table.Column<double>(type: "double precision", nullable: true),
                longitude = table.Column<double>(type: "double precision", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_units", x => x.id);
                table.ForeignKey(
                    name: "fk_units_unit_levels_level_id",
                    column: x => x.level_id,
                    principalTable: "unit_levels",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_units_units_parent_id",
                    column: x => x.parent_id,
                    principalTable: "units",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.InsertData(
            table: "unit_levels",
            columns: new[] { "id", "code", "depth", "name", "plural_name" },
            values: new object[,]
            {
                { new Guid("00000000-0000-0000-0000-000000000001"), 0, 0, "National", "National" },
                { new Guid("00000000-0000-0000-0000-000000000002"), 1, 1, "Zone", "Zones" },
                { new Guid("00000000-0000-0000-0000-000000000003"), 2, 2, "Dila", "Dilaat" },
                { new Guid("00000000-0000-0000-0000-000000000004"), 3, 3, "Muqam", "Muqamaat" },
                { new Guid("00000000-0000-0000-0000-000000000005"), 4, 1, "Department", "Departments" }
            });

        migrationBuilder.InsertData(
            table: "units",
            columns: new[] { "id", "address", "archived_at", "code", "contact_email", "contact_phone", "created_at", "latitude", "level_id", "longitude", "name", "parent_id", "path" },
            values: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), null, null, "NAT", null, null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("00000000-0000-0000-0000-000000000001"), null, "National", null, "nat" });

        migrationBuilder.CreateIndex(
            name: "ix_unit_levels_code",
            table: "unit_levels",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_units_level_id",
            table: "units",
            column: "level_id");

        migrationBuilder.CreateIndex(
            name: "ix_units_parent_id_code",
            table: "units",
            columns: new[] { "parent_id", "code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_units_path",
            table: "units",
            column: "path");

        migrationBuilder.AddForeignKey(
            name: "fk_role_assignments_units_unit_id",
            table: "role_assignments",
            column: "unit_id",
            principalTable: "units",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_role_assignments_units_unit_id",
            table: "role_assignments");

        migrationBuilder.DropTable(
            name: "units");

        migrationBuilder.DropTable(
            name: "unit_levels");
    }
}
