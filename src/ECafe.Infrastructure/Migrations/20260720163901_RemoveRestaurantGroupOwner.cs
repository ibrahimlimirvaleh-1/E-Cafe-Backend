using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRestaurantGroupOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "restaurant_groups_owner_user_id_fkey",
                schema: "core",
                table: "restaurant_groups");

            migrationBuilder.DropIndex(
                name: "IX_restaurant_groups_owner_user_id",
                schema: "core",
                table: "restaurant_groups");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                schema: "core",
                table: "restaurant_groups");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "owner_user_id",
                schema: "core",
                table: "restaurant_groups",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_groups_owner_user_id",
                schema: "core",
                table: "restaurant_groups",
                column: "owner_user_id");

            migrationBuilder.AddForeignKey(
                name: "restaurant_groups_owner_user_id_fkey",
                schema: "core",
                table: "restaurant_groups",
                column: "owner_user_id",
                principalSchema: "auth",
                principalTable: "users",
                principalColumn: "id");
        }
    }
}
