using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartialCancellationStockReturn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StockReturnMode",
                schema: "ops",
                table: "order_cancellation_requests",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "order_cancellation_stock_return_lines",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CancellationRequestId = table.Column<int>(type: "integer", nullable: false),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    UnitId = table.Column<int>(type: "integer", nullable: false),
                    ConsumedQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ReturnQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_cancellation_stock_return_lines", x => x.Id);
                    table.CheckConstraint("cancellation_stock_return_quantity_ck", "\"ConsumedQuantity\" > 0 AND \"ReturnQuantity\" >= 0 AND \"ReturnQuantity\" <= \"ConsumedQuantity\"");
                    table.ForeignKey(
                        name: "FK_order_cancellation_stock_return_lines_inventory_items_Inven~",
                        column: x => x.InventoryItemId,
                        principalSchema: "inventory",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_cancellation_stock_return_lines_order_cancellation_re~",
                        column: x => x.CancellationRequestId,
                        principalSchema: "ops",
                        principalTable: "order_cancellation_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_cancellation_stock_return_lines_units_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "inventory",
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "cancellation_stock_return_mode_ck",
                schema: "ops",
                table: "order_cancellation_requests",
                sql: "\"StockReturnMode\" IS NULL OR \"StockReturnMode\" IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_order_cancellation_stock_return_lines_CancellationRequestId~",
                schema: "ops",
                table: "order_cancellation_stock_return_lines",
                columns: new[] { "CancellationRequestId", "InventoryItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_cancellation_stock_return_lines_InventoryItemId",
                schema: "ops",
                table: "order_cancellation_stock_return_lines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_order_cancellation_stock_return_lines_UnitId",
                schema: "ops",
                table: "order_cancellation_stock_return_lines",
                column: "UnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_cancellation_stock_return_lines",
                schema: "ops");

            migrationBuilder.DropCheckConstraint(
                name: "cancellation_stock_return_mode_ck",
                schema: "ops",
                table: "order_cancellation_requests");

            migrationBuilder.DropColumn(
                name: "StockReturnMode",
                schema: "ops",
                table: "order_cancellation_requests");
        }
    }
}
