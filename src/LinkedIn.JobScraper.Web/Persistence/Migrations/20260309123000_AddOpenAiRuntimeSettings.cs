using System;
using LinkedIn.JobScraper.Web.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkedIn.JobScraper.Web.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(LinkedInJobScraperDbContext))]
    [Migration("20260309123000_AddOpenAiRuntimeSettings")]
    public partial class AddOpenAiRuntimeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OpenAiRuntimeSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettingsKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RequestTimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    UseBackgroundMode = table.Column<bool>(type: "bit", nullable: false),
                    BackgroundPollingIntervalMilliseconds = table.Column<int>(type: "int", nullable: false),
                    BackgroundPollingTimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    MaxConcurrentScoringRequests = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenAiRuntimeSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OpenAiRuntimeSettings_SettingsKey",
                table: "OpenAiRuntimeSettings",
                column: "SettingsKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpenAiRuntimeSettings");
        }
    }
}
