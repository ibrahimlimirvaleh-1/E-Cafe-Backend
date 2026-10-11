using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationReceptionWaiting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reservations_status_id",
                schema: "ops",
                table: "reservations");

            migrationBuilder.AddColumn<int>(
                name: "departure_reason",
                schema: "ops",
                table: "reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "waiting_escalated_at",
                schema: "ops",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "reservations_waiting_lookup_idx",
                schema: "ops",
                table: "reservations",
                columns: new[] { "status_id", "arrived_at", "waiting_escalated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "reservations_waiting_lookup_idx",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "departure_reason",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "waiting_escalated_at",
                schema: "ops",
                table: "reservations");

            migrationBuilder.CreateIndex(
                name: "IX_reservations_status_id",
                schema: "ops",
                table: "reservations",
                column: "status_id");
        }
    }
}
