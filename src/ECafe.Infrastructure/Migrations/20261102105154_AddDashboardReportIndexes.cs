using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardReportIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "reservations_dashboard_period_idx",
                schema: "ops",
                table: "reservations",
                columns: new[] { "restaurant_id", "reserved_at", "id" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "refunds_dashboard_completed_idx",
                schema: "billing",
                table: "reservation_refunds",
                columns: new[] { "refunded_at", "id" },
                filter: "refunded_at IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "payments_dashboard_receipts_idx",
                schema: "billing",
                table: "payments",
                columns: new[] { "restaurant_id", "is_test", "paid_at", "id" },
                filter: "paid_at IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "orders_dashboard_period_idx",
                schema: "ops",
                table: "orders",
                columns: new[] { "restaurant_id", "opened_at", "id" },
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "reservations_dashboard_period_idx",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "refunds_dashboard_completed_idx",
                schema: "billing",
                table: "reservation_refunds");

            migrationBuilder.DropIndex(
                name: "payments_dashboard_receipts_idx",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "orders_dashboard_period_idx",
                schema: "ops",
                table: "orders");

        }
    }
}
