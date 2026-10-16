using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKitchenQueueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "orders_kitchen_queue_idx",
                schema: "ops",
                table: "orders",
                columns: new[] { "restaurant_id", "sent_to_kitchen_at", "id" },
                filter: "sent_to_kitchen_at IS NOT NULL AND closed_at IS NULL AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "orders_kitchen_queue_idx",
                schema: "ops",
                table: "orders");
        }
    }
}
