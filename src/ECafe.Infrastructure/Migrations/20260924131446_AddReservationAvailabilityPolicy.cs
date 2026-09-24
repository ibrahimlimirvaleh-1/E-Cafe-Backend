using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationAvailabilityPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "no_show_grace_minutes",
                schema: "core",
                table: "restaurants",
                type: "integer",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<int>(
                name: "reservation_pre_block_minutes",
                schema: "core",
                table: "restaurants",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "table_turnover_buffer_minutes",
                schema: "core",
                table: "restaurants",
                type: "integer",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<DateTime>(
                name: "must_vacate_at",
                schema: "ops",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "no_show_deadline_at",
                schema: "ops",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql("""
                UPDATE ops.reservations AS reservation
                SET no_show_deadline_at = reservation.reserved_at + make_interval(mins => restaurant.no_show_grace_minutes)
                FROM core.restaurants AS restaurant
                WHERE restaurant.id = reservation.restaurant_id;
                """);

            migrationBuilder.InsertData(
                schema: "core",
                table: "workflow_action_rules",
                columns: new[] { "id", "action_code", "endpoint_template", "flow_code", "http_method", "is_enabled", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[,]
                {
                    { 29, "checkIn", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/check-in", "reservation", "POST", true, "Müştərini check-in et", true, 3, 10, 1002 },
                    { 30, "checkIn", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/check-in", "reservation", "POST", true, "Müştərini check-in et", true, 2, 10, 1002 },
                    { 31, "complete", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/complete", "reservation", "POST", true, "Masa sessiyasını bağla", true, 3, 10, 1003 },
                    { 32, "complete", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/complete", "reservation", "POST", true, "Masa sessiyasını bağla", true, 2, 10, 1003 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 32);

            migrationBuilder.DropColumn(
                name: "no_show_grace_minutes",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "reservation_pre_block_minutes",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "table_turnover_buffer_minutes",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "must_vacate_at",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "no_show_deadline_at",
                schema: "ops",
                table: "reservations");
        }
    }
}
