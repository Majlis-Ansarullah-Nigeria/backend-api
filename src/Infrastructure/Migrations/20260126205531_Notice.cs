using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Notice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Reports");

            migrationBuilder.CreateTable(
                name: "FileAttachments",
                schema: "Reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileData = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileAttachments_ReportSubmissions_ReportSubmissionId",
                        column: x => x.ReportSubmissionId,
                        principalTable: "ReportSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                schema: "Reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationType = table.Column<int>(type: "int", nullable: false),
                    IsInAppEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsEmailEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsPushEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "Reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ReadOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 2),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionComments",
                schema: "Reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentCommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CommenterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommenterName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsEdited = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionComments_ReportSubmissions_ReportSubmissionId",
                        column: x => x.ReportSubmissionId,
                        principalTable: "ReportSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubmissionComments_SubmissionComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalSchema: "Reports",
                        principalTable: "SubmissionComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubmissionFlags",
                schema: "Reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlaggerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlaggerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FlaggedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmissionFlags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmissionFlags_ReportSubmissions_ReportSubmissionId",
                        column: x => x.ReportSubmissionId,
                        principalTable: "ReportSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileAttachments_QuestionId",
                schema: "Reports",
                table: "FileAttachments",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_FileAttachments_ReportSubmissionId",
                schema: "Reports",
                table: "FileAttachments",
                column: "ReportSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId",
                schema: "Reports",
                table: "NotificationPreferences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId_Type",
                schema: "Reports",
                table: "NotificationPreferences",
                columns: new[] { "UserId", "NotificationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedOn",
                schema: "Reports",
                table: "Notifications",
                column: "CreatedOn");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientId",
                schema: "Reports",
                table: "Notifications",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientId_IsRead",
                schema: "Reports",
                table: "Notifications",
                columns: new[] { "RecipientId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RelatedEntity",
                schema: "Reports",
                table: "Notifications",
                columns: new[] { "RelatedEntityId", "RelatedEntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionComments_CommenterId",
                schema: "Reports",
                table: "SubmissionComments",
                column: "CommenterId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionComments_CreatedOn",
                schema: "Reports",
                table: "SubmissionComments",
                column: "CreatedOn");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionComments_ParentCommentId",
                schema: "Reports",
                table: "SubmissionComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionComments_ParentCommentId_CreatedOn",
                schema: "Reports",
                table: "SubmissionComments",
                columns: new[] { "ParentCommentId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionComments_ReportSubmissionId",
                schema: "Reports",
                table: "SubmissionComments",
                column: "ReportSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionComments_ReportSubmissionId_IsDeleted",
                schema: "Reports",
                table: "SubmissionComments",
                columns: new[] { "ReportSubmissionId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionFlags_FlaggedDate",
                schema: "Reports",
                table: "SubmissionFlags",
                column: "FlaggedDate");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionFlags_FlaggerId",
                schema: "Reports",
                table: "SubmissionFlags",
                column: "FlaggerId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionFlags_IsActive",
                schema: "Reports",
                table: "SubmissionFlags",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionFlags_ReportSubmissionId",
                schema: "Reports",
                table: "SubmissionFlags",
                column: "ReportSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmissionFlags_ReportSubmissionId_IsActive",
                schema: "Reports",
                table: "SubmissionFlags",
                columns: new[] { "ReportSubmissionId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileAttachments",
                schema: "Reports");

            migrationBuilder.DropTable(
                name: "NotificationPreferences",
                schema: "Reports");

            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "Reports");

            migrationBuilder.DropTable(
                name: "SubmissionComments",
                schema: "Reports");

            migrationBuilder.DropTable(
                name: "SubmissionFlags",
                schema: "Reports");
        }
    }
}
