using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TemplateDraftsLevelsAndWindowPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStart",
                table: "SubmissionWindows",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "ReportTemplates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubmittingLevelFlags",
                table: "ReportTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReviewStartedByName",
                table: "ReportSubmissions",
                type: "text",
                nullable: true);

            // Templates that existed before drafts were introduced were all live: mark them published.
            migrationBuilder.Sql("UPDATE \"ReportTemplates\" SET \"PublishedAt\" = \"CreatedOn\" WHERE \"PublishedAt\" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PeriodStart",
                table: "SubmissionWindows");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "SubmittingLevelFlags",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "ReviewStartedByName",
                table: "ReportSubmissions");
        }
    }
}
