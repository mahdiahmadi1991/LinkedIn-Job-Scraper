using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkedIn.JobScraper.Web.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorAiGlobalShortlistForSequentialCheckpoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancellationRequestedAtUtc",
                table: "AiGlobalShortlistRuns",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedCount",
                table: "AiGlobalShortlistRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NeedsReviewCount",
                table: "AiGlobalShortlistRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NextSequenceNumber",
                table: "AiGlobalShortlistRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProcessedCount",
                table: "AiGlobalShortlistRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PromptVersion",
                table: "AiGlobalShortlistRuns",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "AiGlobalShortlistItems",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Decision",
                table: "AiGlobalShortlistItems",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                table: "AiGlobalShortlistItems",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InputTokenCount",
                table: "AiGlobalShortlistItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LatencyMilliseconds",
                table: "AiGlobalShortlistItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "AiGlobalShortlistItems",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutputTokenCount",
                table: "AiGlobalShortlistItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptVersion",
                table: "AiGlobalShortlistItems",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalTokenCount",
                table: "AiGlobalShortlistItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiGlobalShortlistRunCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiGlobalShortlistRunCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiGlobalShortlistRunCandidates_AiGlobalShortlistRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "AiGlobalShortlistRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiGlobalShortlistRunCandidates_Jobs_JobRecordId",
                        column: x => x.JobRecordId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiGlobalShortlistItems_RunId_Decision",
                table: "AiGlobalShortlistItems",
                columns: new[] { "RunId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_AiGlobalShortlistRunCandidates_JobRecordId",
                table: "AiGlobalShortlistRunCandidates",
                column: "JobRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGlobalShortlistRunCandidates_RunId",
                table: "AiGlobalShortlistRunCandidates",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGlobalShortlistRunCandidates_RunId_JobRecordId",
                table: "AiGlobalShortlistRunCandidates",
                columns: new[] { "RunId", "JobRecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiGlobalShortlistRunCandidates_RunId_SequenceNumber",
                table: "AiGlobalShortlistRunCandidates",
                columns: new[] { "RunId", "SequenceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiGlobalShortlistRunCandidates");

            migrationBuilder.DropIndex(
                name: "IX_AiGlobalShortlistItems_RunId_Decision",
                table: "AiGlobalShortlistItems");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedAtUtc",
                table: "AiGlobalShortlistRuns");

            migrationBuilder.DropColumn(
                name: "FailedCount",
                table: "AiGlobalShortlistRuns");

            migrationBuilder.DropColumn(
                name: "NeedsReviewCount",
                table: "AiGlobalShortlistRuns");

            migrationBuilder.DropColumn(
                name: "NextSequenceNumber",
                table: "AiGlobalShortlistRuns");

            migrationBuilder.DropColumn(
                name: "ProcessedCount",
                table: "AiGlobalShortlistRuns");

            migrationBuilder.DropColumn(
                name: "PromptVersion",
                table: "AiGlobalShortlistRuns");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "AiGlobalShortlistItems");

            migrationBuilder.DropColumn(
                name: "Decision",
                table: "AiGlobalShortlistItems");

            migrationBuilder.DropColumn(
                name: "ErrorCode",
                table: "AiGlobalShortlistItems");

            migrationBuilder.DropColumn(
                name: "InputTokenCount",
                table: "AiGlobalShortlistItems");

            migrationBuilder.DropColumn(
                name: "LatencyMilliseconds",
                table: "AiGlobalShortlistItems");

            migrationBuilder.DropColumn(
                name: "ModelName",
                table: "AiGlobalShortlistItems");

            migrationBuilder.DropColumn(
                name: "OutputTokenCount",
                table: "AiGlobalShortlistItems");

            migrationBuilder.DropColumn(
                name: "PromptVersion",
                table: "AiGlobalShortlistItems");

            migrationBuilder.DropColumn(
                name: "TotalTokenCount",
                table: "AiGlobalShortlistItems");
        }
    }
}
