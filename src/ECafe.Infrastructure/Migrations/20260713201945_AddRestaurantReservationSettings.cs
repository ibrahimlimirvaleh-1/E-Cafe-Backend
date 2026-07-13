using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantReservationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cancellation_window_minutes",
                schema: "core",
                table: "restaurants",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<decimal>(
                name: "deposit_amount",
                schema: "core",
                table: "restaurants",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "service_fee_percent",
                schema: "core",
                table: "restaurants",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancellation_deadline",
                schema: "ops",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cancellation_window_minutes",
                schema: "ops",
                table: "reservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                schema: "ops",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "deposit_amount",
                schema: "ops",
                table: "reservations",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "no_show_at",
                schema: "ops",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "refund_eligible",
                schema: "ops",
                table: "reservations",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "waiter_user_id",
                schema: "ops",
                table: "reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservations_waiter_user_id",
                schema: "ops",
                table: "reservations",
                column: "waiter_user_id");

            migrationBuilder.AddForeignKey(
                name: "reservations_waiter_user_id_fkey",
                schema: "ops",
                table: "reservations",
                column: "waiter_user_id",
                principalSchema: "auth",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "reservations_waiter_user_id_fkey",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_reservations_waiter_user_id",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "cancellation_window_minutes",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "deposit_amount",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "service_fee_percent",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "cancellation_deadline",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "cancellation_window_minutes",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "deposit_amount",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "no_show_at",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "refund_eligible",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "waiter_user_id",
                schema: "ops",
                table: "reservations");
        }
    }
}
