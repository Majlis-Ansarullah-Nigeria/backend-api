using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWindowReminderSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "escalate_to_parent_after_three_days",
            table: "submission_windows",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "escalate_to_zone_after_seven_days",
            table: "submission_windows",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "remind_five_days_before",
            table: "submission_windows",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "remind_on_due_day",
            table: "submission_windows",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "remind_two_days_before",
            table: "submission_windows",
            type: "boolean",
            nullable: false,
            defaultValue: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "escalate_to_parent_after_three_days",
            table: "submission_windows");

        migrationBuilder.DropColumn(
            name: "escalate_to_zone_after_seven_days",
            table: "submission_windows");

        migrationBuilder.DropColumn(
            name: "remind_five_days_before",
            table: "submission_windows");

        migrationBuilder.DropColumn(
            name: "remind_on_due_day",
            table: "submission_windows");

        migrationBuilder.DropColumn(
            name: "remind_two_days_before",
            table: "submission_windows");
    }
}
