using HRS.Domain.Notifications;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds the notification template for FR-HIS-004's ExportReady event — same hand-authored
/// InsertData/DeleteData convention as SeedNotificationTemplates/
/// SeedEscalationAndAnnouncementTemplates, never editing those merged migrations.
/// </summary>
public partial class SeedExportReadyTemplate : Migration
{
    private static readonly string[] Columns = ["id", "event_type", "subject", "body", "updated_at", "updated_by_user_id"];
    private static readonly DateTimeOffset SeedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly (Guid Id, NotificationEventType EventType, string Subject, string Body)[] Templates =
    [
        (new Guid("00000000-0000-0000-0000-0000000000bc"), NotificationEventType.ExportReady,
            "Your export is ready",
            "Hi,\n\nYour export of {{reportCount}} report(s) is ready to download.\n\n{{link}}"),
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
