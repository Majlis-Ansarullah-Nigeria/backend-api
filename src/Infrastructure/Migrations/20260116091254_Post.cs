using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Post : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberPositions",
                schema: "Membership",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PositionTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OrganizationLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OrganizationEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Responsibilities = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberPositions_Members_MemberId",
                        column: x => x.MemberId,
                        principalSchema: "Membership",
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberPositions_IsActive",
                schema: "Membership",
                table: "MemberPositions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MemberPositions_MemberId",
                schema: "Membership",
                table: "MemberPositions",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberPositions_MemberId_IsActive",
                schema: "Membership",
                table: "MemberPositions",
                columns: new[] { "MemberId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberPositions",
                schema: "Membership");
        }
    }
}
