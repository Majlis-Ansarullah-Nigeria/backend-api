using HRS.Domain.Notifications;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds the two notification templates M8.3 added (FR-NOT-005's Escalated, FR-NOT-006's
/// Announcement) — same hand-authored InsertData/DeleteData convention as M8.1's
/// SeedNotificationTemplates migration, never editing that merged one.
/// </summary>
public partial class SeedEscalationAndAnnouncementTemplates : Migration
{
    private static readonly string[] Columns = ["id", "event_type", "subject", "body", "updated_at", "updated_by_user_id"];
    private static readonly DateTimeOffset SeedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly (Guid Id, NotificationEventType EventType, string Subject, string Body)[] Templates =
    [
        (new Guid("00000000-0000-0000-0000-0000000000ba"), NotificationEventType.Escalated,
            "{{unitName}}'s report is overdue",
            "Hi {{headName}},\n\n{{unitName}}'s report for {{periodLabel}} was due {{dueDate}} and still hasn't been submitted.\n\n{{link}}"),
        (new Guid("00000000-0000-0000-0000-0000000000bb"), NotificationEventType.Announcement,
            "{{title}}",
            "{{message}}\n\n{{link}}"),
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
