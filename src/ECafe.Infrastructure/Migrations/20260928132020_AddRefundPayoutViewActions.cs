using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundPayoutViewActions : Migration
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
                    { 43, "viewPayoutDetails", "/api/v1/restaurants/{restaurantId}/refunds/{refundId}/payout-details", "refund", "GET", true, "Ödəniş rekvizitinə bax", false, 3, 5, 8003 },
                    { 44, "viewPayoutDetails", "/api/v1/restaurants/{restaurantId}/refunds/{refundId}/payout-details", "refund", "GET", true, "Ödəniş rekvizitinə bax", false, 2, 5, 8003 },
                    { 45, "viewPayoutDetails", "/api/v1/restaurants/{restaurantId}/refunds/{refundId}/payout-details", "refund", "GET", true, "Ödəniş rekvizitinə bax", false, 3, 5, 8004 },
                    { 46, "viewPayoutDetails", "/api/v1/restaurants/{restaurantId}/refunds/{refundId}/payout-details", "refund", "GET", true, "Ödəniş rekvizitinə bax", false, 2, 5, 8004 },
                    { 47, "viewPayoutDetails", "/api/v1/restaurants/{restaurantId}/refunds/{refundId}/payout-details", "refund", "GET", true, "Ödəniş rekvizitinə bax", false, 3, 5, 8006 },
                    { 48, "viewPayoutDetails", "/api/v1/restaurants/{restaurantId}/refunds/{refundId}/payout-details", "refund", "GET", true, "Ödəniş rekvizitinə bax", false, 2, 5, 8006 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
