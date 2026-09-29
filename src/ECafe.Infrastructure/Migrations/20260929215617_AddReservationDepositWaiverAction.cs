using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationDepositWaiverAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "core",
                table: "workflow_action_rules",
                columns: new[] { "id", "action_code", "endpoint_template", "flow_code", "http_method", "is_enabled", "label", "requires_confirmation", "requires_reason", "role_id", "sort_order", "status_id" },
                values: new object[,]
                {
                    { 49, "waiveDeposit", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/deposit-waiver", "reservation", "POST", true, "Depozitdən imtina et", true, true, 3, 20, 1010 },
                    { 50, "waiveDeposit", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/deposit-waiver", "reservation", "POST", true, "Depozitdən imtina et", true, true, 2, 20, 1010 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
