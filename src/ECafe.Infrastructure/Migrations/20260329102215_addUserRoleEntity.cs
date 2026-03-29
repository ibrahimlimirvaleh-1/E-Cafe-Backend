using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addUserRoleEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "auth",
                table: "user_roles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                schema: "auth",
                table: "user_roles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                schema: "auth",
                table: "user_roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "auth",
                table: "user_roles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                schema: "auth",
                table: "user_roles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "auth",
                table: "user_roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "auth",
                table: "user_roles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                schema: "auth",
                table: "user_roles",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "user_roles_user_id_key",
                schema: "auth",
                table: "user_roles",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "user_roles_user_id_key",
                schema: "auth",
                table: "user_roles");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "auth",
                table: "user_roles");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                schema: "auth",
                table: "user_roles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "auth",
                table: "user_roles");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "auth",
                table: "user_roles");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "auth",
                table: "user_roles");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "auth",
                table: "user_roles");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "auth",
                table: "user_roles");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                schema: "auth",
                table: "user_roles");
        }
    }
}
