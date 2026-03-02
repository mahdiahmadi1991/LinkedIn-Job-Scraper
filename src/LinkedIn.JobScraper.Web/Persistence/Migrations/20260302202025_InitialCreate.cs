using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkedIn.JobScraper.Web.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiBehaviorSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BehavioralInstructions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrioritySignals = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExclusionSignals = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiBehaviorSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedInJobId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LinkedInJobPostingUrn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LinkedInJobCardUrn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LocationName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmploymentStatus = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompanyApplyUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ListedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FirstDiscoveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CurrentStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AiScore = table.Column<int>(type: "int", nullable: true),
                    AiLabel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AiSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiWhyMatched = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiConcerns = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastScoredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LinkedInSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestHeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastValidatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkedInSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobStatusHistory_Jobs_JobRecordId",
                        column: x => x.JobRecordId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_LinkedInJobId",
                table: "Jobs",
                column: "LinkedInJobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_LinkedInJobPostingUrn",
                table: "Jobs",
                column: "LinkedInJobPostingUrn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobStatusHistory_JobRecordId_ChangedAtUtc",
                table: "JobStatusHistory",
                columns: new[] { "JobRecordId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LinkedInSessions_SessionKey",
                table: "LinkedInSessions",
                column: "SessionKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiBehaviorSettings");

            migrationBuilder.DropTable(
                name: "JobStatusHistory");

            migrationBuilder.DropTable(
                name: "LinkedInSessions");

            migrationBuilder.DropTable(
                name: "Jobs");
        }
    }
}
