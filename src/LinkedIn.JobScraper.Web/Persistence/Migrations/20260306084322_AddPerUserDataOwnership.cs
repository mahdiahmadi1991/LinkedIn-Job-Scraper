using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkedIn.JobScraper.Web.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerUserDataOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LinkedInSessions_SessionKey",
                table: "LinkedInSessions");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_LinkedInJobId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_LinkedInJobPostingUrn",
                table: "Jobs");

            migrationBuilder.AddColumn<int>(
                name: "AppUserId",
                table: "LinkedInSessions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppUserId",
                table: "LinkedInSearchSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppUserId",
                table: "Jobs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppUserId",
                table: "AiGlobalShortlistRuns",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppUserId",
                table: "AiBehaviorSettings",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                DECLARE @LegacyOwnerUserId int;
                SELECT TOP (1) @LegacyOwnerUserId = [Id]
                FROM [AppUsers]
                ORDER BY [Id];

                IF @LegacyOwnerUserId IS NULL
                BEGIN
                    THROW 51000, 'Per-user ownership migration requires at least one AppUsers row. Seed or insert a user, then retry.', 1;
                END;

                UPDATE [LinkedInSessions]
                SET [AppUserId] = @LegacyOwnerUserId
                WHERE [AppUserId] IS NULL;

                UPDATE [LinkedInSearchSettings]
                SET [AppUserId] = @LegacyOwnerUserId
                WHERE [AppUserId] IS NULL;

                UPDATE [Jobs]
                SET [AppUserId] = @LegacyOwnerUserId
                WHERE [AppUserId] IS NULL;

                UPDATE [AiGlobalShortlistRuns]
                SET [AppUserId] = @LegacyOwnerUserId
                WHERE [AppUserId] IS NULL;

                UPDATE [AiBehaviorSettings]
                SET [AppUserId] = @LegacyOwnerUserId
                WHERE [AppUserId] IS NULL;

                IF (SELECT COUNT(*) FROM [LinkedInSearchSettings] WHERE [AppUserId] = @LegacyOwnerUserId) > 1
                BEGIN
                    THROW 51001, 'LinkedInSearchSettings has multiple legacy rows for one user. Resolve duplicates before applying per-user unique constraint.', 1;
                END;

                IF (SELECT COUNT(*) FROM [AiBehaviorSettings] WHERE [AppUserId] = @LegacyOwnerUserId) > 1
                BEGIN
                    THROW 51002, 'AiBehaviorSettings has multiple legacy rows for one user. Resolve duplicates before applying per-user unique constraint.', 1;
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "AppUserId",
                table: "LinkedInSessions",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AppUserId",
                table: "LinkedInSearchSettings",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AppUserId",
                table: "Jobs",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AppUserId",
                table: "AiGlobalShortlistRuns",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AppUserId",
                table: "AiBehaviorSettings",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LinkedInSessions_AppUserId_SessionKey",
                table: "LinkedInSessions",
                columns: new[] { "AppUserId", "SessionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LinkedInSearchSettings_AppUserId",
                table: "LinkedInSearchSettings",
                column: "AppUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_AppUserId",
                table: "Jobs",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_AppUserId_LinkedInJobId",
                table: "Jobs",
                columns: new[] { "AppUserId", "LinkedInJobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_AppUserId_LinkedInJobPostingUrn",
                table: "Jobs",
                columns: new[] { "AppUserId", "LinkedInJobPostingUrn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiGlobalShortlistRuns_AppUserId_CreatedAtUtc",
                table: "AiGlobalShortlistRuns",
                columns: new[] { "AppUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiGlobalShortlistRuns_AppUserId_Status",
                table: "AiGlobalShortlistRuns",
                columns: new[] { "AppUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AiBehaviorSettings_AppUserId",
                table: "AiBehaviorSettings",
                column: "AppUserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AiBehaviorSettings_AppUsers_AppUserId",
                table: "AiBehaviorSettings",
                column: "AppUserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AiGlobalShortlistRuns_AppUsers_AppUserId",
                table: "AiGlobalShortlistRuns",
                column: "AppUserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_AppUsers_AppUserId",
                table: "Jobs",
                column: "AppUserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LinkedInSearchSettings_AppUsers_AppUserId",
                table: "LinkedInSearchSettings",
                column: "AppUserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LinkedInSessions_AppUsers_AppUserId",
                table: "LinkedInSessions",
                column: "AppUserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiBehaviorSettings_AppUsers_AppUserId",
                table: "AiBehaviorSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_AiGlobalShortlistRuns_AppUsers_AppUserId",
                table: "AiGlobalShortlistRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_AppUsers_AppUserId",
                table: "Jobs");

            migrationBuilder.DropForeignKey(
                name: "FK_LinkedInSearchSettings_AppUsers_AppUserId",
                table: "LinkedInSearchSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_LinkedInSessions_AppUsers_AppUserId",
                table: "LinkedInSessions");

            migrationBuilder.DropIndex(
                name: "IX_LinkedInSessions_AppUserId_SessionKey",
                table: "LinkedInSessions");

            migrationBuilder.DropIndex(
                name: "IX_LinkedInSearchSettings_AppUserId",
                table: "LinkedInSearchSettings");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_AppUserId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_AppUserId_LinkedInJobId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_AppUserId_LinkedInJobPostingUrn",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_AiGlobalShortlistRuns_AppUserId_CreatedAtUtc",
                table: "AiGlobalShortlistRuns");

            migrationBuilder.DropIndex(
                name: "IX_AiGlobalShortlistRuns_AppUserId_Status",
                table: "AiGlobalShortlistRuns");

            migrationBuilder.DropIndex(
                name: "IX_AiBehaviorSettings_AppUserId",
                table: "AiBehaviorSettings");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "LinkedInSessions");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "LinkedInSearchSettings");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "AiGlobalShortlistRuns");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "AiBehaviorSettings");

            migrationBuilder.CreateIndex(
                name: "IX_LinkedInSessions_SessionKey",
                table: "LinkedInSessions",
                column: "SessionKey",
                unique: true);

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
        }
    }
}
