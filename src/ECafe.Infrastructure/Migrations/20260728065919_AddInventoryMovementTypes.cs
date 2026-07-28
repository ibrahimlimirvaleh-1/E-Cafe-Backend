using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryMovementTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_movement_types",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("inventory_movement_types_pkey", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "inventory",
                table: "inventory_movement_types",
                columns: new[] { "id", "code", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "description", "IsDeleted", "name", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "Purchase", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, "New stock is purchased and added to inventory.", false, "Purchase", null, null },
                    { 2, "ManualIncrease", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, "Manual inventory increase.", false, "Manual increase", null, null },
                    { 3, "ManualDecrease", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, "Manual inventory decrease.", false, "Manual decrease", null, null },
                    { 4, "OrderConsumption", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, "Stock is consumed when an order item is prepared.", false, "Order consumption", null, null },
                    { 5, "Waste", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, "Damaged or wasted stock is removed from inventory.", false, "Waste", null, null },
                    { 6, "StockReturn", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, "Stock return movement.", false, "Stock return", null, null },
                    { 7, "Correction", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, "Inventory balance correction after count or audit.", false, "Correction", null, null }
                });

            migrationBuilder.AddColumn<int>(
                name: "movement_type_id",
                schema: "inventory",
                table: "inventory_movements",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE inventory.inventory_movements
                SET movement_type_id = CASE movement_type
                    WHEN 'Purchase' THEN 1
                    WHEN 'ManualIncrease' THEN 2
                    WHEN 'ManualDecrease' THEN 3
                    WHEN 'OrderConsumption' THEN 4
                    WHEN 'Waste' THEN 5
                    WHEN 'Return' THEN 6
                    WHEN 'StockReturn' THEN 6
                    WHEN 'Correction' THEN 7
                    ELSE 7
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "movement_type_id",
                schema: "inventory",
                table: "inventory_movements",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "movement_type",
                schema: "inventory",
                table: "inventory_movements");

            migrationBuilder.CreateIndex(
                name: "inventory_movements_movement_type_id_idx",
                schema: "inventory",
                table: "inventory_movements",
                column: "movement_type_id");

            migrationBuilder.CreateIndex(
                name: "inventory_movement_types_code_key",
                schema: "inventory",
                table: "inventory_movement_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "inventory_movement_types_name_key",
                schema: "inventory",
                table: "inventory_movement_types",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "inventory_movements_movement_type_id_fkey",
                schema: "inventory",
                table: "inventory_movements",
                column: "movement_type_id",
                principalSchema: "inventory",
                principalTable: "inventory_movement_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "movement_type",
                schema: "inventory",
                table: "inventory_movements",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE inventory.inventory_movements AS movement
                SET movement_type = COALESCE(type.code, 'Correction')
                FROM inventory.inventory_movement_types AS type
                WHERE movement.movement_type_id = type.id;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "movement_type",
                schema: "inventory",
                table: "inventory_movements",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "inventory_movements_movement_type_id_fkey",
                schema: "inventory",
                table: "inventory_movements");

            migrationBuilder.DropTable(
                name: "inventory_movement_types",
                schema: "inventory");

            migrationBuilder.DropIndex(
                name: "inventory_movements_movement_type_id_idx",
                schema: "inventory",
                table: "inventory_movements");

            migrationBuilder.DropColumn(
                name: "movement_type_id",
                schema: "inventory",
                table: "inventory_movements");
        }
    }
}
