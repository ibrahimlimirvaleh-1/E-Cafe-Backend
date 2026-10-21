using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKitchenRecipeStockGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "inventory_order_consumption_once_idx",
                schema: "inventory",
                table: "inventory_movements",
                columns: new[] { "related_order_item_id", "inventory_item_id" },
                unique: true,
                filter: "movement_type_id = 4 AND related_order_item_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "inventory_order_consumption_once_idx",
                schema: "inventory",
                table: "inventory_movements");
        }
    }
}
