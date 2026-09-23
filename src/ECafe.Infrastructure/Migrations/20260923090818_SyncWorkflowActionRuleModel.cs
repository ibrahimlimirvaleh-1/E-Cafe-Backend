using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncWorkflowActionRuleModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 35);

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

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 44);

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

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 8,
                column: "endpoint_template",
                value: "/api/v1/public/reservations/{reservationId}/cancel");

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 10,
                column: "endpoint_template",
                value: "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel");

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 11,
                columns: new[] { "action_code", "endpoint_template", "label", "requires_confirmation", "role_id", "sort_order" },
                values: new object[] { "cancel", "/api/v1/public/reservations/{reservationId}/cancel", "Rezervasiyanı ləğv et", true, 5, 90 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 12,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[] { "terminate", "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate", "contract", "Müqaviləni ləğv et", true, 1, 90, 6007 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 13,
                columns: new[] { "action_code", "endpoint_template", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[] { "submitPaymentProof", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs", "Ödəniş çekini göndər", false, 5, 10, 1001 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 14,
                columns: new[] { "action_code", "endpoint_template", "label", "role_id", "sort_order", "status_id" },
                values: new object[] { "approvePaymentProof", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs/approve", "Ödənişi təsdiqlə", 3, 10, 1008 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 15,
                columns: new[] { "action_code", "endpoint_template", "label", "role_id", "sort_order", "status_id" },
                values: new object[] { "rejectPaymentProof", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs/reject", "Ödənişi rədd et", 3, 20, 1008 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 16,
                columns: new[] { "action_code", "endpoint_template", "label", "sort_order", "status_id" },
                values: new object[] { "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "Rezervasiyanı ləğv et", 90, 1008 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 17,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[] { "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "Rezervasiyanı ləğv et", true, 2, 90, 1001 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 18,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "role_id", "sort_order", "status_id" },
                values: new object[] { "approvePaymentProof", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs/approve", "reservation", "Ödənişi təsdiqlə", 2, 10, 1008 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 19,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[] { "rejectPaymentProof", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs/reject", "reservation", "Ödənişi rədd et", true, 2, 20, 1008 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 20,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "role_id", "sort_order", "status_id" },
                values: new object[] { "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "Rezervasiyanı ləğv et", 2, 90, 1008 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 21,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "requires_confirmation", "sort_order", "status_id" },
                values: new object[] { "sendPaymentInstruction", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions", "reservation", "Ödəniş məlumatı göndər", false, 10, 1001 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 22,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "role_id", "status_id" },
                values: new object[] { "sendPaymentInstruction", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions", "reservation", "Ödəniş məlumatı göndər", 2, 1001 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 23,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[] { "cancel", "/api/v1/public/reservations/{reservationId}/cancel", "reservation", "Rezervasiyanı ləğv et", true, 5, 90, 1010 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 24,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[] { "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "Rezervasiyanı ləğv et", true, 3, 90, 1010 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 25,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[] { "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "Rezervasiyanı ləğv et", true, 1, 90, 1010 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 26,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "role_id", "sort_order", "status_id" },
                values: new object[] { "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "Rezervasiyanı ləğv et", 2, 90, 1010 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 27,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "requires_confirmation", "sort_order", "status_id" },
                values: new object[] { "sendPaymentInstruction", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions", "reservation", "Ödəniş məlumatı göndər", false, 10, 1010 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 28,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "role_id", "status_id" },
                values: new object[] { "sendPaymentInstruction", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions", "reservation", "Ödəniş məlumatı göndər", 2, 1010 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 8,
                column: "endpoint_template",
                value: "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel");

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 10,
                column: "endpoint_template",
                value: "/api/v1/admin/restaurants/{restaurantId}/reservations/{reservationId}/cancel");

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 11,
                columns: new[] { "action_code", "endpoint_template", "label", "requires_confirmation", "role_id", "sort_order" },
                values: new object[] { "checkIn", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/check-in", "Müştərini oturt", false, 4, 10 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 12,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[] { "checkIn", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/check-in", "reservation", "Müştərini oturt", false, 3, 10, 1002 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 13,
                columns: new[] { "action_code", "endpoint_template", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[] { "markNoShow", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/no-show", "Gəlmədi kimi qeyd et", true, 3, 70, 1002 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 14,
                columns: new[] { "action_code", "endpoint_template", "label", "role_id", "sort_order", "status_id" },
                values: new object[] { "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "Rezervasiyanı ləğv et", 5, 90, 1002 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 15,
                columns: new[] { "action_code", "endpoint_template", "label", "role_id", "sort_order", "status_id" },
                values: new object[] { "complete", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/complete", "Rezervasiyanı tamamla", 4, 80, 1003 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 16,
                columns: new[] { "action_code", "endpoint_template", "label", "sort_order", "status_id" },
                values: new object[] { "complete", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/complete", "Rezervasiyanı tamamla", 80, 1003 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 17,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[] { "sendToKitchen", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/send-to-kitchen", "order", "Mətbəxə göndər", false, 4, 10, 2001 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 18,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "role_id", "sort_order", "status_id" },
                values: new object[] { "cancel", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/cancel", "order", "Sifarişi ləğv et", 3, 90, 2001 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 19,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[] { "serve", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/serve", "order", "Servis edildi", false, 4, 10, 2004 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 20,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "role_id", "sort_order", "status_id" },
                values: new object[] { "close", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/close", "order", "Sifarişi bağla", 4, 80, 2005 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 21,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "requires_confirmation", "sort_order", "status_id" },
                values: new object[] { "close", "/api/v1/restaurants/{restaurantId}/orders/{orderId}/close", "order", "Sifarişi bağla", true, 80, 2005 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 22,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "role_id", "status_id" },
                values: new object[] { "accept", "/api/v1/restaurants/{restaurantId}/kitchen/orders/{orderId}/accept", "kitchen", "Sifarişi qəbul et", 6, 2001 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 23,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[] { "startPreparing", "/api/v1/restaurants/{restaurantId}/kitchen/orders/{orderId}/start", "kitchen", "Hazırlamağa başla", false, 6, 10, 2002 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 24,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[] { "markReady", "/api/v1/restaurants/{restaurantId}/kitchen/orders/{orderId}/ready", "kitchen", "Hazırdır", false, 6, 10, 2003 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 25,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[] { "pay", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/pay", "payment", "Ödəniş et", false, 5, 10, 3001 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 26,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "role_id", "sort_order", "status_id" },
                values: new object[] { "markPaid", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/mark-paid", "payment", "Fiziki ödənişi təsdiqlə", 4, 20, 3001 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 27,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "requires_confirmation", "sort_order", "status_id" },
                values: new object[] { "cancel", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/cancel", "payment", "Ödənişi ləğv et", true, 90, 3001 });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 28,
                columns: new[] { "action_code", "endpoint_template", "flow_code", "label", "role_id", "status_id" },
                values: new object[] { "retry", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/retry", "payment", "Yenidən ödə", 5, 3003 });

            migrationBuilder.InsertData(
                schema: "core",
                table: "workflow_action_rules",
                columns: new[] { "id", "action_code", "endpoint_template", "flow_code", "http_method", "is_enabled", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[,]
                {
                    { 29, "refund", "/api/v1/restaurants/{restaurantId}/payments/{paymentId}/refund", "payment", "POST", true, "Geri qaytar", true, 3, 90, 3002 },
                    { 30, "refund", "/api/v1/admin/restaurants/{restaurantId}/payments/{paymentId}/refund", "payment", "POST", true, "Geri qaytar", true, 1, 90, 3002 },
                    { 31, "terminate", "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate", "contract", "POST", true, "Müqaviləni ləğv et", true, 1, 90, 6007 },
                    { 32, "submitPaymentProof", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs", "reservation", "POST", true, "Ödəniş çekini göndər", false, 5, 10, 1001 },
                    { 33, "approvePaymentProof", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs/approve", "reservation", "POST", true, "Ödənişi təsdiqlə", true, 3, 10, 1008 },
                    { 34, "rejectPaymentProof", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs/reject", "reservation", "POST", true, "Ödənişi rədd et", true, 3, 20, 1008 },
                    { 35, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 3, 90, 1008 },
                    { 36, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 2, 90, 1001 },
                    { 37, "checkIn", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/check-in", "reservation", "POST", true, "Müştərini oturt", false, 2, 10, 1002 },
                    { 38, "markNoShow", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/no-show", "reservation", "POST", true, "Gəlmədi kimi qeyd et", true, 2, 70, 1002 },
                    { 39, "complete", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/complete", "reservation", "POST", true, "Rezervasiyanı tamamla", true, 2, 80, 1003 },
                    { 40, "approvePaymentProof", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs/approve", "reservation", "POST", true, "Ödənişi təsdiqlə", true, 2, 10, 1008 },
                    { 41, "rejectPaymentProof", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs/reject", "reservation", "POST", true, "Ödənişi rədd et", true, 2, 20, 1008 },
                    { 42, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 2, 90, 1008 },
                    { 43, "sendPaymentInstruction", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions", "reservation", "POST", true, "Ödəniş məlumatı göndər", false, 3, 10, 1001 },
                    { 44, "sendPaymentInstruction", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions", "reservation", "POST", true, "Ödəniş məlumatı göndər", false, 2, 10, 1001 },
                    { 45, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 5, 90, 1010 },
                    { 46, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 3, 90, 1010 },
                    { 47, "cancel", "/api/v1/admin/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 1, 90, 1010 },
                    { 48, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 2, 90, 1010 },
                    { 49, "sendPaymentInstruction", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions", "reservation", "POST", true, "Ödəniş məlumatı göndər", false, 3, 10, 1010 },
                    { 50, "sendPaymentInstruction", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions", "reservation", "POST", true, "Ödəniş məlumatı göndər", false, 2, 10, 1010 }
                });
        }
    }
}
