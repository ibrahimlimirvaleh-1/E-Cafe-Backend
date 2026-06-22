using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class deleteItemsFromRestaurant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "items_restaurant_id_fkey",
                schema: "catalog",
                table: "items");

            migrationBuilder.AddForeignKey(
                name: "FK_items_restaurants_restaurant_id",
                schema: "catalog",
                table: "items",
                column: "restaurant_id",
                principalSchema: "core",
                principalTable: "restaurants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_items_restaurants_restaurant_id",
                schema: "catalog",
                table: "items");

            migrationBuilder.AddForeignKey(
                name: "items_restaurant_id_fkey",
                schema: "catalog",
                table: "items",
                column: "restaurant_id",
                principalSchema: "core",
                principalTable: "restaurants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
