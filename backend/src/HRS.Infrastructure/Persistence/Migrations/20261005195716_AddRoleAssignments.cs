using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRoleAssignments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "active_role_assignment_id",
            table: "users",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "role_assignments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                role = table.Column<int>(type: "integer", nullable: false),
                delegate_scope = table.Column<int>(type: "integer", nullable: true),
                starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                appointed_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_role_assignments", x => x.id);
                table.ForeignKey(
                    name: "fk_role_assignments_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_users_active_role_assignment_id",
            table: "users",
            column: "active_role_assignment_id");

        migrationBuilder.CreateIndex(
            name: "ix_role_assignments_unit_id",
            table: "role_assignments",
            column: "unit_id");

        migrationBuilder.CreateIndex(
            name: "ix_role_assignments_user_id",
            table: "role_assignments",
            column: "user_id");

        migrationBuilder.AddForeignKey(
            name: "fk_users_role_assignments_active_role_assignment_id",
            table: "users",
            column: "active_role_assignment_id",
            principalTable: "role_assignments",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_users_role_assignments_active_role_assignment_id",
            table: "users");

        migrationBuilder.DropTable(
            name: "role_assignments");

        migrationBuilder.DropIndex(
            name: "ix_users_active_role_assignment_id",
            table: "users");

        migrationBuilder.DropColumn(
            name: "active_role_assignment_id",
            table: "users");
    }
}
