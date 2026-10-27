using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnusedCancellationStockReturn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IngredientsUnused",
                schema: "ops",
                table: "order_cancellation_requests",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StockReturnedAt",
                schema: "ops",
                table: "order_cancellation_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reversed_movement_id",
                schema: "inventory",
                table: "inventory_movements",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "inventory_movement_reversal_once_idx",
                schema: "inventory",
                table: "inventory_movements",
                column: "reversed_movement_id",
                unique: true,
                filter: "reversed_movement_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "inventory_movement_reversal_valid_ck",
                schema: "inventory",
                table: "inventory_movements",
                sql: "reversed_movement_id IS NULL OR (movement_type_id = 6 AND quantity_change > 0 AND related_order_id IS NOT NULL AND related_order_item_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "inventory_movement_reversal_source_fkey",
                schema: "inventory",
                table: "inventory_movements",
                column: "reversed_movement_id",
                principalSchema: "inventory",
                principalTable: "inventory_movements",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "inventory_movement_reversal_source_fkey",
                schema: "inventory",
                table: "inventory_movements");

            migrationBuilder.DropIndex(
                name: "inventory_movement_reversal_once_idx",
                schema: "inventory",
                table: "inventory_movements");

            migrationBuilder.DropCheckConstraint(
                name: "inventory_movement_reversal_valid_ck",
                schema: "inventory",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "IngredientsUnused",
                schema: "ops",
                table: "order_cancellation_requests");

            migrationBuilder.DropColumn(
                name: "StockReturnedAt",
                schema: "ops",
                table: "order_cancellation_requests");

            migrationBuilder.DropColumn(
                name: "reversed_movement_id",
                schema: "inventory",
                table: "inventory_movements");
        }
    }
}
