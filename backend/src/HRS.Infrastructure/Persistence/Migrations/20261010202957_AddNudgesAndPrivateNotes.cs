using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddNudgesAndPrivateNotes : Migration
{
    private static readonly string[] NudgesUnitIdSentAtColumns = ["unit_id", "sent_at"];
    private static readonly string[] PrivateNotesReportInstanceIdObserverUnitIdColumns = ["report_instance_id", "observer_unit_id"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "nudges",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                sent_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_nudges", x => x.id);
                table.ForeignKey(
                    name: "fk_nudges_units_unit_id",
                    column: x => x.unit_id,
                    principalTable: "units",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "private_notes",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                report_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                observer_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                body = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_private_notes", x => x.id);
                table.ForeignKey(
                    name: "fk_private_notes_report_instances_report_instance_id",
                    column: x => x.report_instance_id,
                    principalTable: "report_instances",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_private_notes_units_observer_unit_id",
                    column: x => x.observer_unit_id,
                    principalTable: "units",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_nudges_unit_id_sent_at",
            table: "nudges",
            columns: NudgesUnitIdSentAtColumns);

        migrationBuilder.CreateIndex(
            name: "ix_private_notes_observer_unit_id",
            table: "private_notes",
            column: "observer_unit_id");

        migrationBuilder.CreateIndex(
            name: "ix_private_notes_report_instance_id_observer_unit_id",
            table: "private_notes",
            columns: PrivateNotesReportInstanceIdObserverUnitIdColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "nudges");

        migrationBuilder.DropTable(
            name: "private_notes");
    }
}
