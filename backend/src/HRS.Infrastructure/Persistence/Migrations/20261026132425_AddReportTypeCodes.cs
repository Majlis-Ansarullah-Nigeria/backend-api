using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddReportTypeCodes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "code",
            table: "report_types",
            type: "character varying(6)",
            maxLength: 6,
            nullable: false,
            defaultValue: "");

        // FR-SUB-010: every existing report type gets its own reference code — its category's
        // code (GMR, QTR, ANN, ADH, EVT, SUR) for the oldest type in each category, so
        // references already issued keep their prefix, then GMR2, GMR3… for the rest. Same rule
        // as ReportTypeCodes.SuggestAsync. Idempotent: only rows still without a code.
        migrationBuilder.Sql("""
            WITH ranked AS (
              SELECT id,
                     CASE category WHEN 0 THEN 'GMR' WHEN 1 THEN 'QTR' WHEN 2 THEN 'ANN'
                                   WHEN 3 THEN 'ADH' WHEN 4 THEN 'EVT' WHEN 5 THEN 'SUR' ELSE 'RPT' END AS base,
                     row_number() OVER (PARTITION BY category ORDER BY created_at, id) AS n
              FROM report_types
              WHERE code = ''
            )
            UPDATE report_types t
            SET code = CASE WHEN r.n = 1 THEN r.base ELSE r.base || r.n END
            FROM ranked r
            WHERE t.id = r.id;
            """);

        migrationBuilder.CreateIndex(
            name: "ix_report_types_code",
            table: "report_types",
            column: "code",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_report_types_code",
            table: "report_types");

        migrationBuilder.DropColumn(
            name: "code",
            table: "report_types");
    }
}
