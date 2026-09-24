using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantReservationDecisionWindows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "payment_hold_minutes",
                schema: "core",
                table: "restaurants",
                type: "integer",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<int>(
                name: "restaurant_response_minutes",
                schema: "core",
                table: "restaurants",
                type: "integer",
                nullable: false,
                defaultValue: 15);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payment_hold_minutes",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "restaurant_response_minutes",
                schema: "core",
                table: "restaurants");
        }
    }
}
