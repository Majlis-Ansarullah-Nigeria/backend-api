using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddReportRevisions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "revision_count",
            table: "report_instances",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "report_revisions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                report_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                revision_no = table.Column<int>(type: "integer", nullable: false),
                action = table.Column<int>(type: "integer", nullable: false),
                answers_json = table.Column<string>(type: "jsonb", nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_report_revisions", x => x.id);
                table.ForeignKey(
                    name: "fk_report_revisions_report_instances_report_instance_id",
                    column: x => x.report_instance_id,
                    principalTable: "report_instances",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_report_revisions_report_instance_id_revision_no",
            table: "report_revisions",
            columns: new[] { "report_instance_id", "revision_no" },
            unique: true);

        // FR-HIS-006 backfill: one Submitted snapshot per report already past submission
        // (Submitted=2, InReview=3, Returned=4, Acknowledged=5), from its current answers —
        // the best record available, since nothing older was kept. Withdrawn/reopened drafts
        // are skipped: their answers may no longer be what was submitted. Idempotent.
        migrationBuilder.Sql("""
            INSERT INTO report_revisions (id, report_instance_id, revision_no, action, answers_json, actor_user_id, created_at)
            SELECT gen_random_uuid(), i.id, 1, 0, i.answers_json, i.submitted_by_user_id, i.submitted_at
            FROM report_instances i
            WHERE i.status IN (2, 3, 4, 5)
              AND i.submitted_at IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM report_revisions r WHERE r.report_instance_id = i.id);

            UPDATE report_instances i
            SET revision_count = (SELECT count(*) FROM report_revisions r WHERE r.report_instance_id = i.id)
            WHERE i.revision_count = 0;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "report_revisions");

        migrationBuilder.DropColumn(
            name: "revision_count",
            table: "report_instances");
    }
}
