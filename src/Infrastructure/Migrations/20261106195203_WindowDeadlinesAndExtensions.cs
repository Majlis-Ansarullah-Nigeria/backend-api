using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WindowDeadlinesAndExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DilaDueAt",
                table: "SubmissionWindows",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MuqamDueAt",
                table: "SubmissionWindows",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ZoneDueAt",
                table: "SubmissionWindows",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WindowExtensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionWindowId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationLevel = table.Column<int>(type: "integer", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosesAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindowExtensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WindowExtensions_SubmissionWindows_SubmissionWindowId",
                        column: x => x.SubmissionWindowId,
                        principalTable: "SubmissionWindows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WindowExtensions_SubmissionWindowId_OrganizationLevel_UnitId",
                table: "WindowExtensions",
                columns: new[] { "SubmissionWindowId", "OrganizationLevel", "UnitId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WindowExtensions");

            migrationBuilder.DropColumn(
                name: "DilaDueAt",
                table: "SubmissionWindows");

            migrationBuilder.DropColumn(
                name: "MuqamDueAt",
                table: "SubmissionWindows");

            migrationBuilder.DropColumn(
                name: "ZoneDueAt",
                table: "SubmissionWindows");
        }
    }
}
