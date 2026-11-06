using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReportTypesAndReferenceNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReportTypeId",
                table: "ReportTemplates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "ReportSubmissions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReportTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportTemplates_ReportTypeId",
                table: "ReportTemplates",
                column: "ReportTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSubmissions_ReferenceNumber",
                table: "ReportSubmissions",
                column: "ReferenceNumber",
                unique: true,
                filter: "\"ReferenceNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReportTypes_Code",
                table: "ReportTypes",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportTemplates_ReportTypes_ReportTypeId",
                table: "ReportTemplates",
                column: "ReportTypeId",
                principalTable: "ReportTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReportTemplates_ReportTypes_ReportTypeId",
                table: "ReportTemplates");

            migrationBuilder.DropTable(
                name: "ReportTypes");

            migrationBuilder.DropIndex(
                name: "IX_ReportTemplates_ReportTypeId",
                table: "ReportTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ReportSubmissions_ReferenceNumber",
                table: "ReportSubmissions");

            migrationBuilder.DropColumn(
                name: "ReportTypeId",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "ReportSubmissions");
        }
    }
}
