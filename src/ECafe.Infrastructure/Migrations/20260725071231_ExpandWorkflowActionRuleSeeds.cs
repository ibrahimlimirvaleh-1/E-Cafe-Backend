using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandWorkflowActionRuleSeeds : Migration
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
                    { 8, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 5, 90, 1001 },
                    { 9, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 3, 90, 1001 },
                    { 10, "cancel", "/api/v1/admin/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 1, 90, 1001 },
                    { 11, "checkIn", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/check-in", "reservation", "POST", true, "Müştərini oturt", false, 4, 10, 1002 },
                    { 12, "checkIn", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/check-in", "reservation", "POST", true, "Müştərini oturt", false, 3, 10, 1002 },
                    { 13, "markNoShow", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/no-show", "reservation", "POST", true, "Gəlmədi kimi qeyd et", true, 3, 70, 1002 },
                    { 14, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 5, 90, 1002 },
                    { 15, "complete", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/complete", "reservation", "POST", true, "Rezervasiyanı tamamla", true, 4, 80, 1003 },
                    { 16, "complete", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/complete", "reservation", "POST", true, "Rezervasiyanı tamamla", true, 3, 80, 1003 },
                    { 17, "sendToKitchen", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/send-to-kitchen", "order", "POST", true, "Mətbəxə göndər", false, 4, 10, 2001 },
                    { 18, "cancel", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/cancel", "order", "POST", true, "Sifarişi ləğv et", true, 3, 90, 2001 },
                    { 19, "serve", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/serve", "order", "POST", true, "Servis edildi", false, 4, 10, 2004 },
                    { 20, "close", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/close", "order", "POST", true, "Sifarişi bağla", true, 4, 80, 2005 },
                    { 21, "close", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/close", "order", "POST", true, "Sifarişi bağla", true, 3, 80, 2005 },
                    { 22, "accept", "/api/v1/restaurants/{restaurantId}/kitchen/orders/{orderId}/accept", "kitchen", "POST", true, "Sifarişi qəbul et", false, 6, 10, 2001 },
                    { 23, "startPreparing", "/api/v1/restaurants/{restaurantId}/kitchen/orders/{orderId}/start", "kitchen", "POST", true, "Hazırlamağa başla", false, 6, 10, 2002 },
                    { 24, "markReady", "/api/v1/restaurants/{restaurantId}/kitchen/orders/{orderId}/ready", "kitchen", "POST", true, "Hazırdır", false, 6, 10, 2003 },
                    { 25, "pay", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/pay", "payment", "POST", true, "Ödəniş et", false, 5, 10, 3001 },
                    { 26, "markPaid", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/mark-paid", "payment", "POST", true, "Fiziki ödənişi təsdiqlə", true, 4, 20, 3001 },
                    { 27, "cancel", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/cancel", "payment", "POST", true, "Ödənişi ləğv et", true, 3, 90, 3001 },
                    { 28, "retry", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/retry", "payment", "POST", true, "Yenidən ödə", false, 5, 10, 3003 },
                    { 29, "refund", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/refund", "payment", "POST", true, "Geri qaytar", true, 3, 90, 3002 },
                    { 30, "refund", "/api/v1/admin/restaurants/{restaurantId}/payments/{paymentId}/refund", "payment", "POST", true, "Geri qaytar", true, 1, 90, 3002 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 28);

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
        }
    }
}
