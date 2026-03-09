using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkedIn.JobScraper.Web.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppUserSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "AppUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AppUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_DeletedAtUtc",
                table: "AppUsers",
                column: "DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_IsDeleted",
                table: "AppUsers",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppUsers_DeletedAtUtc",
                table: "AppUsers");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_IsDeleted",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AppUsers");
        }
    }
}
