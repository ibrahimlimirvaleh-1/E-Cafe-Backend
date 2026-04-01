using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class fixConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "users_file_id_fkey",
                schema: "auth",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_file_id",
                schema: "auth",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "IX_users_file_id",
                schema: "auth",
                table: "users",
                column: "file_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "user_file_id_fkey",
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
                name: "user_file_id_fkey",
                schema: "auth",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_file_id",
                schema: "auth",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "IX_users_file_id",
                schema: "auth",
                table: "users",
                column: "file_id");

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
    }
}
