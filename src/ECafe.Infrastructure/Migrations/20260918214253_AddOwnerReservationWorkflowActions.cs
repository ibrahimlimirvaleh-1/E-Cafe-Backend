using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerReservationWorkflowActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "core",
                table: "workflow_action_rules",
                columns: new[] { "id", "action_code", "endpoint_template", "flow_code", "http_method", "is_enabled", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[,]
                {
                    { 36, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 2, 90, 1001 },
                    { 37, "checkIn", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/check-in", "reservation", "POST", true, "Müştərini oturt", false, 2, 10, 1002 },
                    { 38, "markNoShow", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/no-show", "reservation", "POST", true, "Gəlmədi kimi qeyd et", true, 2, 70, 1002 },
                    { 39, "complete", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/complete", "reservation", "POST", true, "Rezervasiyanı tamamla", true, 2, 80, 1003 },
                    { 40, "approvePaymentProof", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs/approve", "reservation", "POST", true, "Ödənişi təsdiqlə", true, 2, 10, 1008 },
                    { 41, "rejectPaymentProof", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs/reject", "reservation", "POST", true, "Ödənişi rədd et", true, 2, 20, 1008 },
                    { 42, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 2, 90, 1008 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 42);
        }
    }
}
