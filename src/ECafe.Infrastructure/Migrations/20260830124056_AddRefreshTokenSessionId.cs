using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "session_id",
                schema: "auth",
                table: "user_refresh_tokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_user_refresh_tokens_user_id_session_id",
                schema: "auth",
                table: "user_refresh_tokens",
                columns: new[] { "user_id", "session_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_refresh_tokens_user_id_session_id",
                schema: "auth",
                table: "user_refresh_tokens");

            migrationBuilder.DropColumn(
                name: "session_id",
                schema: "auth",
                table: "user_refresh_tokens");
        }
    }
}
