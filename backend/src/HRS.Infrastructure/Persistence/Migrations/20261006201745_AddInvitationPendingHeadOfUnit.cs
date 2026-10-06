using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddInvitationPendingHeadOfUnit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "pending_head_of_unit_id",
            table: "invitations",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_invitations_pending_head_of_unit_id",
            table: "invitations",
            column: "pending_head_of_unit_id");

        migrationBuilder.AddForeignKey(
            name: "fk_invitations_units_pending_head_of_unit_id",
            table: "invitations",
            column: "pending_head_of_unit_id",
            principalTable: "units",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_invitations_units_pending_head_of_unit_id",
            table: "invitations");

        migrationBuilder.DropIndex(
            name: "ix_invitations_pending_head_of_unit_id",
            table: "invitations");

        migrationBuilder.DropColumn(
            name: "pending_head_of_unit_id",
            table: "invitations");
    }
}
