using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixUserRoleIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RoleId",
                schema: "auth",
                table: "users",
                newName: "role_id");

            migrationBuilder.RenameIndex(
                name: "IX_users_RoleId",
                schema: "auth",
                table: "users",
                newName: "IX_users_role_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "role_id",
                schema: "auth",
                table: "users",
                newName: "RoleId");

            migrationBuilder.RenameIndex(
                name: "IX_users_role_id",
                schema: "auth",
                table: "users",
                newName: "IX_users_RoleId");
        }
    }
}
