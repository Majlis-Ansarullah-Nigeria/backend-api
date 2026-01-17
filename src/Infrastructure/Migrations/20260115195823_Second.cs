using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Second : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsForAllMembers",
                table: "ReportTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationLevel",
                table: "ReportTemplates",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsForAllMembers",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "OrganizationLevel",
                table: "ReportTemplates");
        }
    }
}
