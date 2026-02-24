using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "image_url",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "image_url",
                schema: "catalog",
                table: "items");

            migrationBuilder.EnsureSchema(
                name: "common");

            migrationBuilder.AddColumn<int>(
                name: "file_id",
                schema: "auth",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "file_id",
                schema: "catalog",
                table: "items",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "files",
                schema: "common",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    extension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("files_pkey", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_file_id",
                schema: "auth",
                table: "users",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "IX_items_file_id",
                schema: "catalog",
                table: "items",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "files_token_key",
                schema: "common",
                table: "files",
                column: "token",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "items_file_id_fkey",
                schema: "catalog",
                table: "items",
                column: "file_id",
                principalSchema: "common",
                principalTable: "files",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "users_file_id_fkey",
                schema: "auth",
                table: "users",
                column: "file_id",
                principalSchema: "common",
                principalTable: "files",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "items_file_id_fkey",
                schema: "catalog",
                table: "items");

            migrationBuilder.DropForeignKey(
                name: "users_file_id_fkey",
                schema: "auth",
                table: "users");

            migrationBuilder.DropTable(
                name: "files",
                schema: "common");

            migrationBuilder.DropIndex(
                name: "IX_users_file_id",
                schema: "auth",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_items_file_id",
                schema: "catalog",
                table: "items");

            migrationBuilder.DropColumn(
                name: "file_id",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "file_id",
                schema: "catalog",
                table: "items");

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                schema: "auth",
                table: "users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                schema: "catalog",
                table: "items",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }
    }
}
