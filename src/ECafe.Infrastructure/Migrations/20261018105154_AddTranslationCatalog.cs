using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "preferred_language",
                schema: "auth",
                table: "users",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "az");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                schema: "notification",
                table: "notifications",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "az");

            migrationBuilder.AddColumn<string>(
                name: "MessageKey",
                schema: "notification",
                table: "notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleKey",
                schema: "notification",
                table: "notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslationArgumentsJson",
                schema: "notification",
                table: "notifications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslationVersion",
                schema: "notification",
                table: "notifications",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "translation_releases",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessagesJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    SourceReleaseId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_releases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_translation_releases_translation_releases_SourceReleaseId",
                        column: x => x.SourceReleaseId,
                        principalSchema: "core",
                        principalTable: "translation_releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_translation_releases_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "translation_catalog",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    PublishedReleaseId = table.Column<int>(type: "integer", nullable: true),
                    DraftJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_catalog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_translation_catalog_translation_releases_PublishedReleaseId",
                        column: x => x.PublishedReleaseId,
                        principalSchema: "core",
                        principalTable: "translation_releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "core",
                table: "translation_catalog",
                columns: new[] { "Id", "DraftJson", "PublishedReleaseId", "Revision" },
                values: new object[] { 1, "{}", null, 0L });

            migrationBuilder.CreateIndex(
                name: "IX_translation_catalog_PublishedReleaseId",
                schema: "core",
                table: "translation_catalog",
                column: "PublishedReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_translation_releases_CreatedByUserId",
                schema: "core",
                table: "translation_releases",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_translation_releases_SourceReleaseId",
                schema: "core",
                table: "translation_releases",
                column: "SourceReleaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "translation_catalog",
                schema: "core");

            migrationBuilder.DropTable(
                name: "translation_releases",
                schema: "core");

            migrationBuilder.DropColumn(
                name: "preferred_language",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Language",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "MessageKey",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "TitleKey",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "TranslationArgumentsJson",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "TranslationVersion",
                schema: "notification",
                table: "notifications");
        }
    }
}
