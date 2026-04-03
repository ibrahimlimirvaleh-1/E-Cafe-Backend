using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class changeRelationUserAndRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "auth");

            migrationBuilder.AddColumn<int>(
                name: "RoleId",
                schema: "auth",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_users_RoleId",
                schema: "auth",
                table: "users",
                column: "RoleId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_users_roles_RoleId",
                schema: "auth",
                table: "users",
                column: "RoleId",
                principalSchema: "auth",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_roles_RoleId",
                schema: "auth",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_RoleId",
                schema: "auth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RoleId",
                schema: "auth",
                table: "users");

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "auth",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_roles_pkey", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "user_roles_role_id_fkey",
                        column: x => x.role_id,
                        principalSchema: "auth",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "user_roles_user_id_fkey",
                        column: x => x.user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_id",
                schema: "auth",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "user_roles_user_id_key",
                schema: "auth",
                table: "user_roles",
                column: "user_id",
                unique: true);
        }
    }
}
