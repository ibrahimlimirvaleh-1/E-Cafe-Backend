using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameReservationTimeToReservedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "reservations_availability_lookup_idx",
                schema: "ops",
                table: "reservations");

            migrationBuilder.RenameColumn(
                name: "reserved_from",
                schema: "ops",
                table: "reservations",
                newName: "reserved_at");

            migrationBuilder.DropColumn(
                name: "reserved_to",
                schema: "ops",
                table: "reservations");

            migrationBuilder.CreateIndex(
                name: "reservations_availability_lookup_idx",
                schema: "ops",
                table: "reservations",
                columns: new[] { "restaurant_id", "table_id", "status_id", "reserved_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "reservations_availability_lookup_idx",
                schema: "ops",
                table: "reservations");

            migrationBuilder.AddColumn<DateTime>(
                name: "reserved_to",
                schema: "ops",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.RenameColumn(
                name: "reserved_at",
                schema: "ops",
                table: "reservations",
                newName: "reserved_from");

            migrationBuilder.CreateIndex(
                name: "reservations_availability_lookup_idx",
                schema: "ops",
                table: "reservations",
                columns: new[] { "restaurant_id", "table_id", "status_id", "reserved_from", "reserved_to" });
        }
    }
}
