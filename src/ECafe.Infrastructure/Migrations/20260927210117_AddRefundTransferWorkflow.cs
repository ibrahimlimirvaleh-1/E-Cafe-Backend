using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundTransferWorkflow : Migration
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
                    { 37, "submitTransfer", "/api/v1/restaurants/{restaurantId}/refunds/{refundId}/transfers", "refund", "POST", true, "Geri ödəniş çekini göndər", true, 3, 10, 8003 },
                    { 38, "submitTransfer", "/api/v1/restaurants/{restaurantId}/refunds/{refundId}/transfers", "refund", "POST", true, "Geri ödəniş çekini göndər", true, 2, 10, 8003 },
                    { 39, "submitTransfer", "/api/v1/restaurants/{restaurantId}/refunds/{refundId}/transfers", "refund", "POST", true, "Geri ödəniş çekini göndər", true, 3, 10, 8006 },
                    { 40, "submitTransfer", "/api/v1/restaurants/{restaurantId}/refunds/{refundId}/transfers", "refund", "POST", true, "Geri ödəniş çekini göndər", true, 2, 10, 8006 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
