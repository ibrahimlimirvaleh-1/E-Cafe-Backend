using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAndRecipeCostSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ingredient_cost_snapshot",
                schema: "ops",
                table: "order_items",
                type: "numeric(36,8)",
                precision: 36,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cost_amount",
                schema: "inventory",
                table: "inventory_movements",
                type: "numeric(26,8)",
                precision: 26,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "stock_cost_value",
                schema: "inventory",
                table: "inventory_items",
                type: "numeric(26,8)",
                precision: 26,
                scale: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ingredient_cost_snapshot",
                schema: "ops",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "cost_amount",
                schema: "inventory",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "stock_cost_value",
                schema: "inventory",
                table: "inventory_items");
        }
    }
}
