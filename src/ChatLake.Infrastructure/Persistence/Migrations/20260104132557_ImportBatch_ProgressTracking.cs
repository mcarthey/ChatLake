using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatLake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImportBatch_ProgressTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "ImportBatch",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "ImportBatch",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastHeartbeatUtc",
                table: "ImportBatch",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessedConversationCount",
                table: "ImportBatch",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtUtc",
                table: "ImportBatch",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalConversationCount",
                table: "ImportBatch",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "ImportBatch");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "ImportBatch");

            migrationBuilder.DropColumn(
                name: "LastHeartbeatUtc",
                table: "ImportBatch");

            migrationBuilder.DropColumn(
                name: "ProcessedConversationCount",
                table: "ImportBatch");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "ImportBatch");

            migrationBuilder.DropColumn(
                name: "TotalConversationCount",
                table: "ImportBatch");
        }
    }
}
