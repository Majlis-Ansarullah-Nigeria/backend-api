using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddInvitationPendingTermDates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "pending_ends_at",
            table: "invitations",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "pending_starts_at",
            table: "invitations",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "pending_ends_at",
            table: "invitations");

        migrationBuilder.DropColumn(
            name: "pending_starts_at",
            table: "invitations");
    }
}
