using HRS.Domain.Notifications;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds one default, editable template per FR-NOT-002 event (FR-NOT-003) so the dispatcher
/// always has something to render, even before the Secretariat customizes any wording. A plain
/// data migration (InsertData, not HasData) — same reasoning as M6.3's default report template:
/// ordinary Secretariat-editable rows from the moment they exist, not permanent schema.
/// </summary>
public partial class SeedNotificationTemplates : Migration
{
    private static readonly string[] Columns = ["id", "event_type", "subject", "body", "updated_at", "updated_by_user_id"];
    private static readonly DateTimeOffset SeedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly (Guid Id, NotificationEventType EventType, string Subject, string Body)[] Templates =
    [
        (new Guid("00000000-0000-0000-0000-0000000000b1"), NotificationEventType.WindowOpened,
            "A new reporting window is open for {{unitName}}",
            "Hi {{headName}},\n\nThe {{unitName}} window is now open. It's due {{dueDate}}.\n\nFile it here: {{link}}"),
        (new Guid("00000000-0000-0000-0000-0000000000b2"), NotificationEventType.Reminder,
            "Reminder: your {{unitName}} report is due {{dueDate}}",
            "Hi {{headName}},\n\nThis is a reminder that your report for {{unitName}} is due {{dueDate}}.\n\nContinue here: {{link}}"),
        (new Guid("00000000-0000-0000-0000-0000000000b3"), NotificationEventType.Overdue,
            "Overdue: your {{unitName}} report",
            "Hi {{headName}},\n\nYour report for {{unitName}} was due {{dueDate}} and hasn't been submitted yet. Please submit it as soon as you can.\n\n{{link}}"),
        (new Guid("00000000-0000-0000-0000-0000000000b4"), NotificationEventType.ExtensionGranted,
            "Extension granted for {{unitName}}",
            "Hi {{headName}},\n\n{{unitName}}'s deadline has been extended to {{dueDate}}. Reason: {{reason}}.\n\n{{link}}"),
        (new Guid("00000000-0000-0000-0000-0000000000b5"), NotificationEventType.ReportAcknowledged,
            "{{unitName}}'s report has been acknowledged",
            "Hi {{headName}},\n\nThe National Secretariat has acknowledged {{unitName}}'s report.\n\n{{link}}"),
        (new Guid("00000000-0000-0000-0000-0000000000b6"), NotificationEventType.ReturnedForRevision,
            "{{unitName}}'s report was returned for revision",
            "Hi {{headName}},\n\nThe National Secretariat has returned {{unitName}}'s report for revision:\n\n{{comment}}\n\n{{link}}"),
        (new Guid("00000000-0000-0000-0000-0000000000b7"), NotificationEventType.CommentAdded,
            "New comment on {{unitName}}'s report",
            "Hi {{headName}},\n\nA new comment was posted on {{unitName}}'s report:\n\n{{comment}}\n\n{{link}}"),
        (new Guid("00000000-0000-0000-0000-0000000000b8"), NotificationEventType.ChildSubmitted,
            "{{unitName}} just submitted their report",
            "Hi {{headName}},\n\n{{unitName}} has submitted their report. You can review it here:\n\n{{link}}"),
        (new Guid("00000000-0000-0000-0000-0000000000b9"), NotificationEventType.TemplateChanged,
            "The template for {{unitName}}'s report has changed",
            "Hi {{headName}},\n\nThe template for {{unitName}}'s in-progress report has been updated by the National Secretariat. Please review your draft before resubmitting.\n\n{{link}}"),
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var template in Templates)
        {
            migrationBuilder.InsertData(
                table: "notification_templates",
                columns: Columns,
                values: new object[] { template.Id, (int)template.EventType, template.Subject, template.Body, SeedTimestamp, null });
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var template in Templates)
        {
            migrationBuilder.DeleteData(table: "notification_templates", keyColumn: "id", keyValue: template.Id);
        }
    }
}
