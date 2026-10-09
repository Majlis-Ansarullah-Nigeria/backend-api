using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <summary>
/// A no-op by design. xmin is Postgres' own system column — it already exists on every table.
/// EF Core's migration generator doesn't know that and generates an AddColumn/DropColumn pair
/// for a column named "xmin"; applying that would fail (or at best shadow the real system
/// column), so both operations are stripped. Only the C# mapping
/// (AppDbContext: .Property(i => i.RowVersion).HasColumnName("xmin").IsRowVersion()) matters —
/// this migration exists solely so the model snapshot and the database's migration history
/// stay in sync.
/// </summary>
public partial class AddReportInstanceRowVersion : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
