using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatLake.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial_Bronze : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportBatch",
                columns: table => new
                {
                    ImportBatchId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ImportLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ArtifactCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatch", x => x.ImportBatchId);
                });

            migrationBuilder.CreateTable(
                name: "RawArtifact",
                columns: table => new
                {
                    RawArtifactId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportBatchId = table.Column<long>(type: "bigint", nullable: false),
                    ArtifactType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ArtifactName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ByteLength = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    RawJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StoredPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawArtifact", x => x.RawArtifactId);
                    table.ForeignKey(
                        name: "FK_RawArtifact_ImportBatch_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatch",
                        principalColumn: "ImportBatchId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatch_ImportedAtUtc",
                table: "ImportBatch",
                column: "ImportedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RawArtifact_ImportBatchId",
                table: "RawArtifact",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_RawArtifact_Sha256",
                table: "RawArtifact",
                column: "Sha256");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawArtifact");

            migrationBuilder.DropTable(
                name: "ImportBatch");
        }
    }
}
