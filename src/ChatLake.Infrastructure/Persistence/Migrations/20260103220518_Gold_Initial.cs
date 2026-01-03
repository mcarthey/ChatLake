using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatLake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Gold_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationSummary",
                columns: table => new
                {
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    MessageCount = table.Column<int>(type: "int", nullable: false),
                    FirstMessageAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastMessageAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ParticipantSet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PreviewText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationSummary", x => x.ConversationId);
                    table.ForeignKey(
                        name: "FK_ConversationSummary_Conversation_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversation",
                        principalColumn: "ConversationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Project",
                columns: table => new
                {
                    ProjectId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsSystemGenerated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Project", x => x.ProjectId);
                    table.CheckConstraint("CK_Project_Status", "[Status] IN ('Active','Archived')");
                });

            migrationBuilder.CreateTable(
                name: "ProjectConversation",
                columns: table => new
                {
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AddedBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectConversation", x => new { x.ProjectId, x.ConversationId });
                    table.CheckConstraint("CK_ProjectConversation_AddedBy", "[AddedBy] IN ('Manual','System')");
                    table.ForeignKey(
                        name: "FK_ProjectConversation_Conversation_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversation",
                        principalColumn: "ConversationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectConversation_Project_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Project",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Project_Name",
                table: "Project",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectConversation_ConversationId",
                table: "ProjectConversation",
                column: "ConversationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationSummary");

            migrationBuilder.DropTable(
                name: "ProjectConversation");

            migrationBuilder.DropTable(
                name: "Project");
        }
    }
}
