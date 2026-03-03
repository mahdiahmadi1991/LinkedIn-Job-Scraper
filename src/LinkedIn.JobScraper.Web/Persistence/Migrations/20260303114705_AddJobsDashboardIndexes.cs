using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkedIn.JobScraper.Web.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobsDashboardIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Jobs_AiLabel",
                table: "Jobs",
                column: "AiLabel");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_AiScore",
                table: "Jobs",
                column: "AiScore");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_CurrentStatus",
                table: "Jobs",
                column: "CurrentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_LastSeenAtUtc",
                table: "Jobs",
                column: "LastSeenAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ListedAtUtc",
                table: "Jobs",
                column: "ListedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_AiLabel",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_AiScore",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_CurrentStatus",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_LastSeenAtUtc",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_ListedAtUtc",
                table: "Jobs");
        }
    }
}
