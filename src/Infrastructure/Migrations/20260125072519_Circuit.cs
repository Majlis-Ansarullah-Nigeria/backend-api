using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Circuit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CircuitName",
                schema: "Organization",
                table: "Jamaats",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CircuitName",
                schema: "Organization",
                table: "Jamaats");
        }
    }
}
