using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationResponseDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "restaurant_response_expires_at",
                schema: "ops",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.InsertData(
                schema: "auth",
                table: "statuses",
                columns: new[] { "id", "blocks_table_availability", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "status_type_id", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 1010, true, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Restoran cavabı gözlənilir", 1, null, null });

            migrationBuilder.InsertData(
                schema: "core",
                table: "workflow_action_rules",
                columns: new[] { "id", "action_code", "endpoint_template", "flow_code", "http_method", "is_enabled", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[,]
                {
                    { 45, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 5, 90, 1010 },
                    { 46, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 3, 90, 1010 },
                    { 47, "cancel", "/api/v1/admin/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 1, 90, 1010 },
                    { 48, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 2, 90, 1010 },
                    { 49, "sendPaymentInstruction", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions", "reservation", "POST", true, "Ödəniş məlumatı göndər", false, 3, 10, 1010 },
                    { 50, "sendPaymentInstruction", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions", "reservation", "POST", true, "Ödəniş məlumatı göndər", false, 2, 10, 1010 }
                });

            migrationBuilder.CreateIndex(
                name: "reservations_restaurant_response_expires_at_idx",
                schema: "ops",
                table: "reservations",
                column: "restaurant_response_expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "reservations_restaurant_response_expires_at_idx",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 1010);

            migrationBuilder.DropColumn(
                name: "restaurant_response_expires_at",
                schema: "ops",
                table: "reservations");
        }
    }
}
