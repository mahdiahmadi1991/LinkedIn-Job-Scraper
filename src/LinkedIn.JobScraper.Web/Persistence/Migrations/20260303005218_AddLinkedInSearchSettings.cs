using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkedIn.JobScraper.Web.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkedInSearchSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LinkedInSearchSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Keywords = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    LocationInput = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LocationDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LocationGeoId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    EasyApply = table.Column<bool>(type: "bit", nullable: false),
                    WorkplaceTypeCodesCsv = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    JobTypeCodesCsv = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkedInSearchSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LinkedInSearchSettings");
        }
    }
}
