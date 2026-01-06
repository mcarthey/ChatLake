using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatLake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SegmentLevelClustering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SegmentIdsJson",
                table: "ProjectSuggestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UniqueConversationCount",
                table: "ProjectSuggestion",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ConversationSegment",
                columns: table => new
                {
                    ConversationSegmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    SegmentIndex = table.Column<int>(type: "int", nullable: false),
                    StartMessageIndex = table.Column<int>(type: "int", nullable: false),
                    EndMessageIndex = table.Column<int>(type: "int", nullable: false),
                    MessageCount = table.Column<int>(type: "int", nullable: false),
                    ContentText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    InferenceRunId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationSegment", x => x.ConversationSegmentId);
                    table.ForeignKey(
                        name: "FK_ConversationSegment_Conversation_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversation",
                        principalColumn: "ConversationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationSegment_InferenceRun_InferenceRunId",
                        column: x => x.InferenceRunId,
                        principalTable: "InferenceRun",
                        principalColumn: "InferenceRunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SegmentEmbedding",
                columns: table => new
                {
                    SegmentEmbeddingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationSegmentId = table.Column<long>(type: "bigint", nullable: false),
                    InferenceRunId = table.Column<long>(type: "bigint", nullable: false),
                    EmbeddingModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Dimensions = table.Column<int>(type: "int", nullable: false),
                    EmbeddingVector = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    SourceContentHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SegmentEmbedding", x => x.SegmentEmbeddingId);
                    table.ForeignKey(
                        name: "FK_SegmentEmbedding_ConversationSegment_ConversationSegmentId",
                        column: x => x.ConversationSegmentId,
                        principalTable: "ConversationSegment",
                        principalColumn: "ConversationSegmentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SegmentEmbedding_InferenceRun_InferenceRunId",
                        column: x => x.InferenceRunId,
                        principalTable: "InferenceRun",
                        principalColumn: "InferenceRunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSegment_ConversationId",
                table: "ConversationSegment",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSegment_InferenceRunId",
                table: "ConversationSegment",
                column: "InferenceRunId");

            migrationBuilder.CreateIndex(
                name: "UQ_ConversationSegment_Position",
                table: "ConversationSegment",
                columns: new[] { "ConversationId", "SegmentIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SegmentEmbedding_InferenceRunId",
                table: "SegmentEmbedding",
                column: "InferenceRunId");

            migrationBuilder.CreateIndex(
                name: "UQ_SegmentEmbedding_Segment_Model",
                table: "SegmentEmbedding",
                columns: new[] { "ConversationSegmentId", "EmbeddingModel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SegmentEmbedding");

            migrationBuilder.DropTable(
                name: "ConversationSegment");

            migrationBuilder.DropColumn(
                name: "SegmentIdsJson",
                table: "ProjectSuggestion");

            migrationBuilder.DropColumn(
                name: "UniqueConversationCount",
                table: "ProjectSuggestion");
        }
    }
}
