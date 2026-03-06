using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkedIn.JobScraper.Web.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobDetailSyncColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DetailContentFingerprint",
                table: "Jobs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastDetailSyncedAtUtc",
                table: "Jobs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LinkedInUpdatedAtUtc",
                table: "Jobs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_LastDetailSyncedAtUtc",
                table: "Jobs",
                column: "LastDetailSyncedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_LinkedInUpdatedAtUtc",
                table: "Jobs",
                column: "LinkedInUpdatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_LastDetailSyncedAtUtc",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_LinkedInUpdatedAtUtc",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "DetailContentFingerprint",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "LastDetailSyncedAtUtc",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "LinkedInUpdatedAtUtc",
                table: "Jobs");
        }
    }
}
