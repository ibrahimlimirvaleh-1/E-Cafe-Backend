using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FilterUserUniqueIndexesForSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "users_email_key",
                schema: "auth",
                table: "users");

            migrationBuilder.DropIndex(
                name: "users_phone_key",
                schema: "auth",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "users_email_key",
                schema: "auth",
                table: "users",
                column: "email",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "users_phone_key",
                schema: "auth",
                table: "users",
                column: "phone",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "users_email_key",
                schema: "auth",
                table: "users");

            migrationBuilder.DropIndex(
                name: "users_phone_key",
                schema: "auth",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "users_email_key",
                schema: "auth",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "users_phone_key",
                schema: "auth",
                table: "users",
                column: "phone",
                unique: true);
        }
    }
}
