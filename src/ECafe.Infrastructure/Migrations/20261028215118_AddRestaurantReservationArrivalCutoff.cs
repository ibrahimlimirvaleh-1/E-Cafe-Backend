using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantReservationArrivalCutoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "last_reservation_arrival_at",
                schema: "core",
                table: "restaurant_working_hours",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "last_reservation_arrival_day_offset",
                schema: "core",
                table: "restaurant_working_hours",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reservation_arrival_cutoff_at",
                schema: "ops",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_restaurant_working_hours_arrival_cutoff",
                schema: "core",
                table: "restaurant_working_hours",
                sql: "(last_reservation_arrival_at IS NULL AND last_reservation_arrival_day_offset IS NULL) OR (last_reservation_arrival_at IS NOT NULL AND last_reservation_arrival_day_offset IS NOT NULL AND is_closed = false AND last_reservation_arrival_day_offset BETWEEN 0 AND 1 AND EXTRACT(EPOCH FROM last_reservation_arrival_at) + last_reservation_arrival_day_offset * 86400 >= EXTRACT(EPOCH FROM opens_at) AND EXTRACT(EPOCH FROM last_reservation_arrival_at) + last_reservation_arrival_day_offset * 86400 < EXTRACT(EPOCH FROM closes_at) + close_day_offset * 86400)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reservations_arrival_cutoff",
                schema: "ops",
                table: "reservations",
                sql: "reservation_arrival_cutoff_at IS NULL OR reservation_arrival_cutoff_at >= reserved_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_restaurant_working_hours_arrival_cutoff",
                schema: "core",
                table: "restaurant_working_hours");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reservations_arrival_cutoff",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "last_reservation_arrival_at",
                schema: "core",
                table: "restaurant_working_hours");

            migrationBuilder.DropColumn(
                name: "last_reservation_arrival_day_offset",
                schema: "core",
                table: "restaurant_working_hours");

            migrationBuilder.DropColumn(
                name: "reservation_arrival_cutoff_at",
                schema: "ops",
                table: "reservations");
        }
    }
}
