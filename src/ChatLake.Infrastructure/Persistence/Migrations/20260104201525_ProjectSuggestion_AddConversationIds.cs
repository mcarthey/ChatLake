using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatLake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectSuggestion_AddConversationIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConversationIdsJson",
                table: "ProjectSuggestion",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConversationIdsJson",
                table: "ProjectSuggestion");
        }
    }
}
