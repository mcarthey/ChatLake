using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatLake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GoldTier_InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ProjectConversation",
                table: "ProjectConversation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectConversation_AddedBy",
                table: "ProjectConversation");

            migrationBuilder.DropIndex(
                name: "UX_Project_Name",
                table: "Project");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Project_Status",
                table: "Project");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Project");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Project");

            migrationBuilder.RenameColumn(
                name: "AddedBy",
                table: "ProjectConversation",
                newName: "AssignedBy");

            migrationBuilder.RenameColumn(
                name: "AddedAtUtc",
                table: "ProjectConversation",
                newName: "AssignedAtUtc");

            migrationBuilder.AddColumn<long>(
                name: "ProjectConversationId",
                table: "ProjectConversation",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<decimal>(
                name: "Confidence",
                table: "ProjectConversation",
                type: "decimal(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "InferenceRunId",
                table: "ProjectConversation",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrent",
                table: "ProjectConversation",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Project",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Project",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Project",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Project",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ProjectKey",
                table: "Project",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProjectConversation",
                table: "ProjectConversation",
                column: "ProjectConversationId");

            migrationBuilder.CreateTable(
                name: "InferenceRun",
                columns: table => new
                {
                    InferenceRunId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FeatureConfigHashSha256 = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    InputScope = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InputDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MetricsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InferenceRun", x => x.InferenceRunId);
                });

            migrationBuilder.CreateTable(
                name: "UserOverride",
                columns: table => new
                {
                    UserOverrideId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OverrideType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetId = table.Column<long>(type: "bigint", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserOverride", x => x.UserOverrideId);
                });

            migrationBuilder.CreateTable(
                name: "BlogTopicSuggestion",
                columns: table => new
                {
                    BlogTopicSuggestionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InferenceRunId = table.Column<long>(type: "bigint", nullable: false),
                    ProjectId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OutlineJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    SourceConversationIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogTopicSuggestion", x => x.BlogTopicSuggestionId);
                    table.ForeignKey(
                        name: "FK_BlogTopicSuggestion_InferenceRun_InferenceRunId",
                        column: x => x.InferenceRunId,
                        principalTable: "InferenceRun",
                        principalColumn: "InferenceRunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BlogTopicSuggestion_Project_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Project",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ConversationSimilarity",
                columns: table => new
                {
                    ConversationSimilarityId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InferenceRunId = table.Column<long>(type: "bigint", nullable: false),
                    ConversationIdA = table.Column<long>(type: "bigint", nullable: false),
                    ConversationIdB = table.Column<long>(type: "bigint", nullable: false),
                    Similarity = table.Column<decimal>(type: "decimal(7,6)", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationSimilarity", x => x.ConversationSimilarityId);
                    table.ForeignKey(
                        name: "FK_ConversationSimilarity_InferenceRun_InferenceRunId",
                        column: x => x.InferenceRunId,
                        principalTable: "InferenceRun",
                        principalColumn: "InferenceRunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectDriftMetric",
                columns: table => new
                {
                    ProjectDriftMetricId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InferenceRunId = table.Column<long>(type: "bigint", nullable: false),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    WindowStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DriftScore = table.Column<decimal>(type: "decimal(7,6)", nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDriftMetric", x => x.ProjectDriftMetricId);
                    table.ForeignKey(
                        name: "FK_ProjectDriftMetric_InferenceRun_InferenceRunId",
                        column: x => x.InferenceRunId,
                        principalTable: "InferenceRun",
                        principalColumn: "InferenceRunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectDriftMetric_Project_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Project",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectSuggestion",
                columns: table => new
                {
                    ProjectSuggestionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InferenceRunId = table.Column<long>(type: "bigint", nullable: false),
                    SuggestedProjectKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SuggestedName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResolvedProjectId = table.Column<long>(type: "bigint", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSuggestion", x => x.ProjectSuggestionId);
                    table.ForeignKey(
                        name: "FK_ProjectSuggestion_InferenceRun_InferenceRunId",
                        column: x => x.InferenceRunId,
                        principalTable: "InferenceRun",
                        principalColumn: "InferenceRunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectSuggestion_Project_ResolvedProjectId",
                        column: x => x.ResolvedProjectId,
                        principalTable: "Project",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Topic",
                columns: table => new
                {
                    TopicId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InferenceRunId = table.Column<long>(type: "bigint", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    KeywordsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topic", x => x.TopicId);
                    table.ForeignKey(
                        name: "FK_Topic_InferenceRun_InferenceRunId",
                        column: x => x.InferenceRunId,
                        principalTable: "InferenceRun",
                        principalColumn: "InferenceRunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationTopic",
                columns: table => new
                {
                    ConversationTopicId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InferenceRunId = table.Column<long>(type: "bigint", nullable: false),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    TopicId = table.Column<long>(type: "bigint", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(7,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationTopic", x => x.ConversationTopicId);
                    table.ForeignKey(
                        name: "FK_ConversationTopic_InferenceRun_InferenceRunId",
                        column: x => x.InferenceRunId,
                        principalTable: "InferenceRun",
                        principalColumn: "InferenceRunId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationTopic_Topic_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topic",
                        principalColumn: "TopicId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectConversation_InferenceRunId",
                table: "ProjectConversation",
                column: "InferenceRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectConversation_ProjectId",
                table: "ProjectConversation",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "UQ_ProjectConversation_Current",
                table: "ProjectConversation",
                columns: new[] { "ProjectId", "ConversationId", "IsCurrent" },
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "UQ_Project_ProjectKey",
                table: "Project",
                column: "ProjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlogTopicSuggestion_Confidence",
                table: "BlogTopicSuggestion",
                column: "Confidence");

            migrationBuilder.CreateIndex(
                name: "IX_BlogTopicSuggestion_InferenceRunId",
                table: "BlogTopicSuggestion",
                column: "InferenceRunId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogTopicSuggestion_ProjectId",
                table: "BlogTopicSuggestion",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_BlogTopicSuggestion_Status",
                table: "BlogTopicSuggestion",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSimilarity_A",
                table: "ConversationSimilarity",
                column: "ConversationIdA");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSimilarity_B",
                table: "ConversationSimilarity",
                column: "ConversationIdB");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSimilarity_Value",
                table: "ConversationSimilarity",
                column: "Similarity");

            migrationBuilder.CreateIndex(
                name: "UQ_ConversationSimilarity_Pair",
                table: "ConversationSimilarity",
                columns: new[] { "InferenceRunId", "ConversationIdA", "ConversationIdB" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationTopic_ConversationId",
                table: "ConversationTopic",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationTopic_InferenceRunId",
                table: "ConversationTopic",
                column: "InferenceRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationTopic_TopicId",
                table: "ConversationTopic",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_InferenceRun_RunType",
                table: "InferenceRun",
                column: "RunType");

            migrationBuilder.CreateIndex(
                name: "IX_InferenceRun_StartedAtUtc",
                table: "InferenceRun",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDriftMetric_InferenceRunId",
                table: "ProjectDriftMetric",
                column: "InferenceRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDriftMetric_Project_Window",
                table: "ProjectDriftMetric",
                columns: new[] { "ProjectId", "WindowStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSuggestion_Confidence",
                table: "ProjectSuggestion",
                column: "Confidence");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSuggestion_InferenceRunId",
                table: "ProjectSuggestion",
                column: "InferenceRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSuggestion_ResolvedProjectId",
                table: "ProjectSuggestion",
                column: "ResolvedProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSuggestion_Status",
                table: "ProjectSuggestion",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Topic_InferenceRunId",
                table: "Topic",
                column: "InferenceRunId");

            migrationBuilder.CreateIndex(
                name: "IX_UserOverride_CreatedAtUtc",
                table: "UserOverride",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UserOverride_Target",
                table: "UserOverride",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectConversation_InferenceRun_InferenceRunId",
                table: "ProjectConversation",
                column: "InferenceRunId",
                principalTable: "InferenceRun",
                principalColumn: "InferenceRunId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectConversation_InferenceRun_InferenceRunId",
                table: "ProjectConversation");

            migrationBuilder.DropTable(
                name: "BlogTopicSuggestion");

            migrationBuilder.DropTable(
                name: "ConversationSimilarity");

            migrationBuilder.DropTable(
                name: "ConversationTopic");

            migrationBuilder.DropTable(
                name: "ProjectDriftMetric");

            migrationBuilder.DropTable(
                name: "ProjectSuggestion");

            migrationBuilder.DropTable(
                name: "UserOverride");

            migrationBuilder.DropTable(
                name: "Topic");

            migrationBuilder.DropTable(
                name: "InferenceRun");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProjectConversation",
                table: "ProjectConversation");

            migrationBuilder.DropIndex(
                name: "IX_ProjectConversation_InferenceRunId",
                table: "ProjectConversation");

            migrationBuilder.DropIndex(
                name: "IX_ProjectConversation_ProjectId",
                table: "ProjectConversation");

            migrationBuilder.DropIndex(
                name: "UQ_ProjectConversation_Current",
                table: "ProjectConversation");

            migrationBuilder.DropIndex(
                name: "UQ_Project_ProjectKey",
                table: "Project");

            migrationBuilder.DropColumn(
                name: "ProjectConversationId",
                table: "ProjectConversation");

            migrationBuilder.DropColumn(
                name: "Confidence",
                table: "ProjectConversation");

            migrationBuilder.DropColumn(
                name: "InferenceRunId",
                table: "ProjectConversation");

            migrationBuilder.DropColumn(
                name: "IsCurrent",
                table: "ProjectConversation");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Project");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Project");

            migrationBuilder.DropColumn(
                name: "ProjectKey",
                table: "Project");

            migrationBuilder.RenameColumn(
                name: "AssignedBy",
                table: "ProjectConversation",
                newName: "AddedBy");

            migrationBuilder.RenameColumn(
                name: "AssignedAtUtc",
                table: "ProjectConversation",
                newName: "AddedAtUtc");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Project",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Project",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Project",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Project",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProjectConversation",
                table: "ProjectConversation",
                columns: new[] { "ProjectId", "ConversationId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectConversation_AddedBy",
                table: "ProjectConversation",
                sql: "[AddedBy] IN ('Manual','System')");

            migrationBuilder.CreateIndex(
                name: "UX_Project_Name",
                table: "Project",
                column: "Name",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Project_Status",
                table: "Project",
                sql: "[Status] IN ('Active','Archived')");
        }
    }
}
