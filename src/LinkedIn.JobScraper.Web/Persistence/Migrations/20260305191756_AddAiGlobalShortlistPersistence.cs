using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkedIn.JobScraper.Web.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiGlobalShortlistPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiGlobalShortlistRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CandidateCount = table.Column<int>(type: "int", nullable: false),
                    ShortlistedCount = table.Column<int>(type: "int", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiGlobalShortlistRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiGlobalShortlistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: true),
                    Confidence = table.Column<int>(type: "int", nullable: true),
                    RecommendationReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Concerns = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiGlobalShortlistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiGlobalShortlistItems_AiGlobalShortlistRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "AiGlobalShortlistRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiGlobalShortlistItems_Jobs_JobRecordId",
                        column: x => x.JobRecordId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiGlobalShortlistItems_JobRecordId",
                table: "AiGlobalShortlistItems",
                column: "JobRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGlobalShortlistItems_RunId",
                table: "AiGlobalShortlistItems",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGlobalShortlistItems_RunId_JobRecordId",
                table: "AiGlobalShortlistItems",
                columns: new[] { "RunId", "JobRecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiGlobalShortlistItems_RunId_Rank",
                table: "AiGlobalShortlistItems",
                columns: new[] { "RunId", "Rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiGlobalShortlistRuns_CompletedAtUtc",
                table: "AiGlobalShortlistRuns",
                column: "CompletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AiGlobalShortlistRuns_CreatedAtUtc",
                table: "AiGlobalShortlistRuns",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AiGlobalShortlistRuns_Status",
                table: "AiGlobalShortlistRuns",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiGlobalShortlistItems");

            migrationBuilder.DropTable(
                name: "AiGlobalShortlistRuns");
        }
    }
}
