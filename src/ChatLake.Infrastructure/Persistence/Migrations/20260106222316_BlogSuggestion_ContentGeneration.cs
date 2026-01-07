using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatLake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BlogSuggestion_ContentGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlogContentMarkdown",
                table: "BlogTopicSuggestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationScoreJson",
                table: "BlogTopicSuggestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GeneratedAtUtc",
                table: "BlogTopicSuggestion",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProjectSuggestionId",
                table: "BlogTopicSuggestion",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSegmentIdsJson",
                table: "BlogTopicSuggestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WordCount",
                table: "BlogTopicSuggestion",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlogTopicSuggestion_ProjectSuggestionId",
                table: "BlogTopicSuggestion",
                column: "ProjectSuggestionId");

            migrationBuilder.AddForeignKey(
                name: "FK_BlogTopicSuggestion_ProjectSuggestion_ProjectSuggestionId",
                table: "BlogTopicSuggestion",
                column: "ProjectSuggestionId",
                principalTable: "ProjectSuggestion",
                principalColumn: "ProjectSuggestionId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlogTopicSuggestion_ProjectSuggestion_ProjectSuggestionId",
                table: "BlogTopicSuggestion");

            migrationBuilder.DropIndex(
                name: "IX_BlogTopicSuggestion_ProjectSuggestionId",
                table: "BlogTopicSuggestion");

            migrationBuilder.DropColumn(
                name: "BlogContentMarkdown",
                table: "BlogTopicSuggestion");

            migrationBuilder.DropColumn(
                name: "EvaluationScoreJson",
                table: "BlogTopicSuggestion");

            migrationBuilder.DropColumn(
                name: "GeneratedAtUtc",
                table: "BlogTopicSuggestion");

            migrationBuilder.DropColumn(
                name: "ProjectSuggestionId",
                table: "BlogTopicSuggestion");

            migrationBuilder.DropColumn(
                name: "SourceSegmentIdsJson",
                table: "BlogTopicSuggestion");

            migrationBuilder.DropColumn(
                name: "WordCount",
                table: "BlogTopicSuggestion");
        }
    }
}
