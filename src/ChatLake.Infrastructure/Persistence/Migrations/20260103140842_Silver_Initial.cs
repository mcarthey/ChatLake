using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatLake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Silver_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Conversation",
                columns: table => new
                {
                    ConversationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationKey = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExternalConversationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FirstMessageAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastMessageAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedFromImportBatchId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversation", x => x.ConversationId);
                    table.ForeignKey(
                        name: "FK_Conversation_ImportBatch_CreatedFromImportBatchId",
                        column: x => x.CreatedFromImportBatchId,
                        principalTable: "ImportBatch",
                        principalColumn: "ImportBatchId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParsingFailure",
                columns: table => new
                {
                    ParsingFailureId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RawArtifactId = table.Column<long>(type: "bigint", nullable: false),
                    FailureStage = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FailureMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParsingFailure", x => x.ParsingFailureId);
                    table.ForeignKey(
                        name: "FK_ParsingFailure_RawArtifact_RawArtifactId",
                        column: x => x.RawArtifactId,
                        principalTable: "RawArtifact",
                        principalColumn: "RawArtifactId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationArtifactMap",
                columns: table => new
                {
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    RawArtifactId = table.Column<long>(type: "bigint", nullable: false),
                    MappedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationArtifactMap", x => new { x.ConversationId, x.RawArtifactId });
                    table.ForeignKey(
                        name: "FK_ConversationArtifactMap_Conversation_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversation",
                        principalColumn: "ConversationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationArtifactMap_RawArtifact_RawArtifactId",
                        column: x => x.RawArtifactId,
                        principalTable: "RawArtifact",
                        principalColumn: "RawArtifactId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Message",
                columns: table => new
                {
                    MessageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SequenceIndex = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    MessageTimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RawArtifactId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Message", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_Message_Conversation_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversation",
                        principalColumn: "ConversationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Message_RawArtifact_RawArtifactId",
                        column: x => x.RawArtifactId,
                        principalTable: "RawArtifact",
                        principalColumn: "RawArtifactId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conversation_CreatedFromImportBatchId",
                table: "Conversation",
                column: "CreatedFromImportBatchId");

            migrationBuilder.CreateIndex(
                name: "UX_Conversation_ConversationKey",
                table: "Conversation",
                column: "ConversationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationArtifactMap_RawArtifactId",
                table: "ConversationArtifactMap",
                column: "RawArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_Message_ConversationId",
                table: "Message",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Message_RawArtifactId",
                table: "Message",
                column: "RawArtifactId");

            migrationBuilder.CreateIndex(
                name: "UX_Message_Conversation_Role_Sequence_ContentHash",
                table: "Message",
                columns: new[] { "ConversationId", "Role", "SequenceIndex", "ContentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParsingFailure_RawArtifactId",
                table: "ParsingFailure",
                column: "RawArtifactId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationArtifactMap");

            migrationBuilder.DropTable(
                name: "Message");

            migrationBuilder.DropTable(
                name: "ParsingFailure");

            migrationBuilder.DropTable(
                name: "Conversation");
        }
    }
}
