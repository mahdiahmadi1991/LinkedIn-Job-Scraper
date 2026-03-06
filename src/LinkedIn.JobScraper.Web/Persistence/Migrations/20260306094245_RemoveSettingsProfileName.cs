using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkedIn.JobScraper.Web.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSettingsProfileName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfileName",
                table: "LinkedInSearchSettings");

            migrationBuilder.DropColumn(
                name: "ProfileName",
                table: "AiBehaviorSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfileName",
                table: "LinkedInSearchSettings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProfileName",
                table: "AiBehaviorSettings",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");
        }
    }
}
