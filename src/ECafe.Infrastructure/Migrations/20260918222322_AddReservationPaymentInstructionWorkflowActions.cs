using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationPaymentInstructionWorkflowActions : Migration
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
                    { 43, "sendPaymentInstruction", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions", "reservation", "POST", true, "Ödəniş məlumatı göndər", false, 3, 10, 1001 },
                    { 44, "sendPaymentInstruction", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-instructions", "reservation", "POST", true, "Ödəniş məlumatı göndər", false, 2, 10, 1001 }
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
        }
    }
}
