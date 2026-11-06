using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReportReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportReminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionWindowId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationLevel = table.Column<int>(type: "integer", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    DaysBeforeDue = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportReminders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportReminders_SubmissionWindowId_OrganizationLevel_UnitId~",
                table: "ReportReminders",
                columns: new[] { "SubmissionWindowId", "OrganizationLevel", "UnitId", "DaysBeforeDue" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportReminders");
        }
    }
}
