using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddInvitationPendingRoleAndUserDeactivation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "deactivated_at",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "appointed_by",
            table: "invitations",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "pending_delegate_scope",
            table: "invitations",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "pending_role",
            table: "invitations",
            type: "integer",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "deactivated_at",
            table: "users");

        migrationBuilder.DropColumn(
            name: "appointed_by",
            table: "invitations");

        migrationBuilder.DropColumn(
            name: "pending_delegate_scope",
            table: "invitations");

        migrationBuilder.DropColumn(
            name: "pending_role",
            table: "invitations");
    }
}
