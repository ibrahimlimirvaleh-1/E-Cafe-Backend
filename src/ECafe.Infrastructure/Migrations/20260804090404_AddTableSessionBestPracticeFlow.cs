using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTableSessionBestPracticeFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "user_restaurants_user_id_key",
                schema: "auth",
                table: "user_restaurants");

            migrationBuilder.DropIndex(
                name: "IX_orders_restaurant_id",
                schema: "ops",
                table: "orders");

            migrationBuilder.RenameIndex(
                name: "IX_user_restaurants_restaurant_id",
                schema: "auth",
                table: "user_restaurants",
                newName: "user_restaurants_restaurant_id_idx");

            migrationBuilder.AddColumn<int>(
                name: "cancelled_by_user_id",
                schema: "ops",
                table: "reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "checked_in_by_user_id",
                schema: "ops",
                table: "reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "completed_by_user_id",
                schema: "ops",
                table: "reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "no_show_by_user_id",
                schema: "ops",
                table: "reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "accepted_at",
                schema: "ops",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "customer_user_id",
                schema: "ops",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "preparing_at",
                schema: "ops",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ready_at",
                schema: "ops",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "scheduled_kitchen_time",
                schema: "ops",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "sent_to_kitchen_at",
                schema: "ops",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "served_at",
                schema: "ops",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source_id",
                schema: "ops",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "table_session_id",
                schema: "ops",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "status_id",
                schema: "ops",
                table: "order_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "related_order_item_id",
                schema: "inventory",
                table: "inventory_movements",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "table_sessions",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    restaurant_id = table.Column<int>(type: "integer", nullable: false),
                    table_id = table.Column<int>(type: "integer", nullable: false),
                    customer_user_id = table.Column<int>(type: "integer", nullable: true),
                    waiter_user_id = table.Column<int>(type: "integer", nullable: true),
                    reservation_id = table.Column<int>(type: "integer", nullable: true),
                    status_id = table.Column<int>(type: "integer", nullable: false),
                    opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("table_sessions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "table_sessions_customer_user_id_fkey",
                        column: x => x.customer_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "table_sessions_reservation_id_fkey",
                        column: x => x.reservation_id,
                        principalSchema: "ops",
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "table_sessions_restaurant_id_fkey",
                        column: x => x.restaurant_id,
                        principalSchema: "core",
                        principalTable: "restaurants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "table_sessions_status_id_fkey",
                        column: x => x.status_id,
                        principalSchema: "auth",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "table_sessions_table_id_fkey",
                        column: x => x.table_id,
                        principalSchema: "ops",
                        principalTable: "tables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "table_sessions_waiter_user_id_fkey",
                        column: x => x.waiter_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                schema: "auth",
                table: "status_types",
                columns: new[] { "id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 7, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Masa sessiyası statusları", null, null });

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 2006,
                column: "name",
                value: "Sifariş bağlanıb");

            migrationBuilder.InsertData(
                schema: "auth",
                table: "statuses",
                columns: new[] { "id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "status_type_id", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 2008, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Sifariş planlaşdırılıb", 2, null, null },
                    { 7001, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Masa sessiyası açıqdır", 7, null, null },
                    { 7002, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Masa sessiyası bağlanıb", 7, null, null },
                    { 7003, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Masa sessiyası ləğv edilib", 7, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "user_restaurants_user_id_idx",
                schema: "auth",
                table: "user_restaurants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservations_cancelled_by_user_id",
                schema: "ops",
                table: "reservations",
                column: "cancelled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservations_checked_in_by_user_id",
                schema: "ops",
                table: "reservations",
                column: "checked_in_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservations_completed_by_user_id",
                schema: "ops",
                table: "reservations",
                column: "completed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservations_no_show_by_user_id",
                schema: "ops",
                table: "reservations",
                column: "no_show_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_customer_user_id",
                schema: "ops",
                table: "orders",
                column: "customer_user_id");

            migrationBuilder.CreateIndex(
                name: "orders_restaurant_status_scheduled_kitchen_time_idx",
                schema: "ops",
                table: "orders",
                columns: new[] { "restaurant_id", "status_id", "scheduled_kitchen_time" });

            migrationBuilder.CreateIndex(
                name: "orders_table_session_id_idx",
                schema: "ops",
                table: "orders",
                column: "table_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_status_id",
                schema: "ops",
                table: "order_items",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "inventory_movements_related_order_item_id_idx",
                schema: "inventory",
                table: "inventory_movements",
                column: "related_order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_table_sessions_status_id",
                schema: "ops",
                table: "table_sessions",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "IX_table_sessions_table_id",
                schema: "ops",
                table: "table_sessions",
                column: "table_id");

            migrationBuilder.CreateIndex(
                name: "table_sessions_customer_user_id_idx",
                schema: "ops",
                table: "table_sessions",
                column: "customer_user_id");

            migrationBuilder.CreateIndex(
                name: "table_sessions_reservation_id_idx",
                schema: "ops",
                table: "table_sessions",
                column: "reservation_id");

            migrationBuilder.CreateIndex(
                name: "table_sessions_restaurant_table_status_idx",
                schema: "ops",
                table: "table_sessions",
                columns: new[] { "restaurant_id", "table_id", "status_id" });

            migrationBuilder.CreateIndex(
                name: "table_sessions_waiter_user_id_idx",
                schema: "ops",
                table: "table_sessions",
                column: "waiter_user_id");

            migrationBuilder.AddForeignKey(
                name: "inventory_movements_related_order_item_id_fkey",
                schema: "inventory",
                table: "inventory_movements",
                column: "related_order_item_id",
                principalSchema: "ops",
                principalTable: "order_items",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "order_items_status_id_fkey",
                schema: "ops",
                table: "order_items",
                column: "status_id",
                principalSchema: "auth",
                principalTable: "statuses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "orders_customer_user_id_fkey",
                schema: "ops",
                table: "orders",
                column: "customer_user_id",
                principalSchema: "auth",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "orders_table_session_id_fkey",
                schema: "ops",
                table: "orders",
                column: "table_session_id",
                principalSchema: "ops",
                principalTable: "table_sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "reservations_cancelled_by_user_id_fkey",
                schema: "ops",
                table: "reservations",
                column: "cancelled_by_user_id",
                principalSchema: "auth",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "reservations_checked_in_by_user_id_fkey",
                schema: "ops",
                table: "reservations",
                column: "checked_in_by_user_id",
                principalSchema: "auth",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "reservations_completed_by_user_id_fkey",
                schema: "ops",
                table: "reservations",
                column: "completed_by_user_id",
                principalSchema: "auth",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "reservations_no_show_by_user_id_fkey",
                schema: "ops",
                table: "reservations",
                column: "no_show_by_user_id",
                principalSchema: "auth",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "inventory_movements_related_order_item_id_fkey",
                schema: "inventory",
                table: "inventory_movements");

            migrationBuilder.DropForeignKey(
                name: "order_items_status_id_fkey",
                schema: "ops",
                table: "order_items");

            migrationBuilder.DropForeignKey(
                name: "orders_customer_user_id_fkey",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "orders_table_session_id_fkey",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "reservations_cancelled_by_user_id_fkey",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropForeignKey(
                name: "reservations_checked_in_by_user_id_fkey",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropForeignKey(
                name: "reservations_completed_by_user_id_fkey",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropForeignKey(
                name: "reservations_no_show_by_user_id_fkey",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropTable(
                name: "table_sessions",
                schema: "ops");

            migrationBuilder.DropIndex(
                name: "user_restaurants_user_id_idx",
                schema: "auth",
                table: "user_restaurants");

            migrationBuilder.DropIndex(
                name: "IX_reservations_cancelled_by_user_id",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_reservations_checked_in_by_user_id",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_reservations_completed_by_user_id",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_reservations_no_show_by_user_id",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_orders_customer_user_id",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "orders_restaurant_status_scheduled_kitchen_time_idx",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "orders_table_session_id_idx",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_order_items_status_id",
                schema: "ops",
                table: "order_items");

            migrationBuilder.DropIndex(
                name: "inventory_movements_related_order_item_id_idx",
                schema: "inventory",
                table: "inventory_movements");

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 2008);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 7001);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 7002);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 7003);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "status_types",
                keyColumn: "id",
                keyValue: 7);

            migrationBuilder.DropColumn(
                name: "cancelled_by_user_id",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "checked_in_by_user_id",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "completed_by_user_id",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "no_show_by_user_id",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "accepted_at",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "customer_user_id",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "preparing_at",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ready_at",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "scheduled_kitchen_time",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "sent_to_kitchen_at",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "served_at",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "source_id",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "table_session_id",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "status_id",
                schema: "ops",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "related_order_item_id",
                schema: "inventory",
                table: "inventory_movements");

            migrationBuilder.RenameIndex(
                name: "user_restaurants_restaurant_id_idx",
                schema: "auth",
                table: "user_restaurants",
                newName: "IX_user_restaurants_restaurant_id");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 2006,
                column: "name",
                value: "Sifariş online ödənişlə bağlanıb");

            migrationBuilder.CreateIndex(
                name: "user_restaurants_user_id_key",
                schema: "auth",
                table: "user_restaurants",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_restaurant_id",
                schema: "ops",
                table: "orders",
                column: "restaurant_id");
        }
    }
}
