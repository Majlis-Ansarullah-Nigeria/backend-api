using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReportUnitOwnershipAndReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportSubmissions_SubmissionWindowId",
                table: "ReportSubmissions");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "ReportSubmissions");

            migrationBuilder.AddColumn<DateTime>(
                name: "AcknowledgedAt",
                table: "ReportSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLate",
                table: "ReportSubmissions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReturnReason",
                table: "ReportSubmissions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedAt",
                table: "ReportSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewStartedAt",
                table: "ReportSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevisionDueAt",
                table: "ReportSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                table: "ReportSubmissions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ReportSubmissions_DilaId",
                table: "ReportSubmissions",
                column: "DilaId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSubmissions_Status_SubmittedAt",
                table: "ReportSubmissions",
                columns: new[] { "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportSubmissions_Window_Unit",
                table: "ReportSubmissions",
                columns: new[] { "SubmissionWindowId", "OrganizationLevel", "UnitId" },
                unique: true,
                filter: "\"SubmissionWindowId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSubmissions_ZoneId",
                table: "ReportSubmissions",
                column: "ZoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportSubmissions_DilaId",
                table: "ReportSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_ReportSubmissions_Status_SubmittedAt",
                table: "ReportSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_ReportSubmissions_Window_Unit",
                table: "ReportSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_ReportSubmissions_ZoneId",
                table: "ReportSubmissions");

            migrationBuilder.DropColumn(
                name: "AcknowledgedAt",
                table: "ReportSubmissions");

            migrationBuilder.DropColumn(
                name: "IsLate",
                table: "ReportSubmissions");

            migrationBuilder.DropColumn(
                name: "ReturnReason",
                table: "ReportSubmissions");

            migrationBuilder.DropColumn(
                name: "ReturnedAt",
                table: "ReportSubmissions");

            migrationBuilder.DropColumn(
                name: "ReviewStartedAt",
                table: "ReportSubmissions");

            migrationBuilder.DropColumn(
                name: "RevisionDueAt",
                table: "ReportSubmissions");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "ReportSubmissions");

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ReportSubmissions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportSubmissions_SubmissionWindowId",
                table: "ReportSubmissions",
                column: "SubmissionWindowId");
        }
    }
}
