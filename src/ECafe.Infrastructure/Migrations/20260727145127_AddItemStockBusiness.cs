using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddItemStockBusiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "units",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    base_unit_id = table.Column<int>(type: "integer", nullable: true),
                    conversion_rate_to_base = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false, defaultValue: 1m),
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
                    table.PrimaryKey("units_pkey", x => x.id);
                    table.ForeignKey(
                        name: "units_base_unit_id_fkey",
                        column: x => x.base_unit_id,
                        principalSchema: "inventory",
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_items",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    restaurant_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    unit_id = table.Column<int>(type: "integer", nullable: false),
                    quantity_on_hand = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false, defaultValue: 0m),
                    low_stock_threshold = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false, defaultValue: 0m),
                    last_low_stock_notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("inventory_items_pkey", x => x.id);
                    table.ForeignKey(
                        name: "inventory_items_restaurant_id_fkey",
                        column: x => x.restaurant_id,
                        principalSchema: "core",
                        principalTable: "restaurants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "inventory_items_unit_id_fkey",
                        column: x => x.unit_id,
                        principalSchema: "inventory",
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_movements",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    restaurant_id = table.Column<int>(type: "integer", nullable: false),
                    inventory_item_id = table.Column<int>(type: "integer", nullable: false),
                    quantity_change = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_id = table.Column<int>(type: "integer", nullable: false),
                    movement_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    related_order_id = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("inventory_movements_pkey", x => x.id);
                    table.ForeignKey(
                        name: "inventory_movements_inventory_item_id_fkey",
                        column: x => x.inventory_item_id,
                        principalSchema: "inventory",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "inventory_movements_related_order_id_fkey",
                        column: x => x.related_order_id,
                        principalSchema: "ops",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "inventory_movements_restaurant_id_fkey",
                        column: x => x.restaurant_id,
                        principalSchema: "core",
                        principalTable: "restaurants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "inventory_movements_unit_id_fkey",
                        column: x => x.unit_id,
                        principalSchema: "inventory",
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recipes",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    restaurant_id = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    inventory_item_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_id = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("recipes_pkey", x => x.id);
                    table.ForeignKey(
                        name: "recipes_inventory_item_id_fkey",
                        column: x => x.inventory_item_id,
                        principalSchema: "inventory",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "recipes_item_id_fkey",
                        column: x => x.item_id,
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "recipes_restaurant_id_fkey",
                        column: x => x.restaurant_id,
                        principalSchema: "core",
                        principalTable: "restaurants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "recipes_unit_id_fkey",
                        column: x => x.unit_id,
                        principalSchema: "inventory",
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "auth",
                table: "permissions",
                columns: new[] { "id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 22, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Stoka baxmaq", null, null },
                    { 23, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Stoku idarə etmək", null, null },
                    { 24, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Reseptlərə baxmaq", null, null },
                    { 25, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Reseptləri idarə etmək", null, null }
                });

            migrationBuilder.InsertData(
                schema: "inventory",
                table: "units",
                columns: new[] { "id", "base_unit_id", "code", "conversion_rate_to_base", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, null, "kg", 1m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Kiloqram", null, null },
                    { 3, null, "l", 1m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Litr", null, null },
                    { 5, null, "pcs", 1m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Ədəd", null, null }
                });

            migrationBuilder.InsertData(
                schema: "auth",
                table: "role_permisions",
                columns: new[] { "permission_id", "role_id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Id", "IsDeleted", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 22, 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 23, 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 24, 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 25, 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 22, 2, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 24, 2, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 22, 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 23, 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 24, 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 22, 6, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 24, 6, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 25, 6, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null }
                });

            migrationBuilder.InsertData(
                schema: "inventory",
                table: "units",
                columns: new[] { "id", "base_unit_id", "code", "conversion_rate_to_base", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 2, 1, "g", 0.001m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Qram", null, null },
                    { 4, 3, "ml", 0.001m, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Millilitr", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "inventory_items_restaurant_id_is_active_idx",
                schema: "inventory",
                table: "inventory_items",
                columns: new[] { "restaurant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "inventory_items_restaurant_id_name_key",
                schema: "inventory",
                table: "inventory_items",
                columns: new[] { "restaurant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_unit_id",
                schema: "inventory",
                table: "inventory_items",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "inventory_movements_related_order_id_idx",
                schema: "inventory",
                table: "inventory_movements",
                column: "related_order_id");

            migrationBuilder.CreateIndex(
                name: "inventory_movements_restaurant_item_created_at_idx",
                schema: "inventory",
                table: "inventory_movements",
                columns: new[] { "restaurant_id", "inventory_item_id", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_inventory_item_id",
                schema: "inventory",
                table: "inventory_movements",
                column: "inventory_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_movements_unit_id",
                schema: "inventory",
                table: "inventory_movements",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_recipes_inventory_item_id",
                schema: "inventory",
                table: "recipes",
                column: "inventory_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_recipes_item_id",
                schema: "inventory",
                table: "recipes",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_recipes_unit_id",
                schema: "inventory",
                table: "recipes",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "recipes_restaurant_id_item_id_idx",
                schema: "inventory",
                table: "recipes",
                columns: new[] { "restaurant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "recipes_restaurant_item_inventory_key",
                schema: "inventory",
                table: "recipes",
                columns: new[] { "restaurant_id", "item_id", "inventory_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_units_base_unit_id",
                schema: "inventory",
                table: "units",
                column: "base_unit_id");

            migrationBuilder.CreateIndex(
                name: "units_code_key",
                schema: "inventory",
                table: "units",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "units_name_key",
                schema: "inventory",
                table: "units",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_movements",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "recipes",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "inventory_items",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "units",
                schema: "inventory");

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 22, 1 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 23, 1 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 24, 1 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 25, 1 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 22, 2 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 24, 2 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 22, 3 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 23, 3 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 24, 3 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 22, 6 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 24, 6 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 25, 6 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 25);
        }
    }
}
