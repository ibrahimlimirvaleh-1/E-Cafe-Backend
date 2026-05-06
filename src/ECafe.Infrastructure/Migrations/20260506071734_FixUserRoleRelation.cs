using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixUserRoleRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_RoleId",
                schema: "auth",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "IX_users_RoleId",
                schema: "auth",
                table: "users",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_RoleId",
                schema: "auth",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "IX_users_RoleId",
                schema: "auth",
                table: "users",
                column: "RoleId",
                unique: true);
        }
    }
}
