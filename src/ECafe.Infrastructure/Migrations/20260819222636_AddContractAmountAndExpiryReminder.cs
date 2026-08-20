using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContractAmountAndExpiryReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_restaurant_contracts_status_id",
                schema: "core",
                table: "restaurant_contracts");

            migrationBuilder.AddColumn<decimal>(
                name: "amount",
                schema: "core",
                table: "restaurant_contracts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "expiry_reminder_at",
                schema: "core",
                table: "restaurant_contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "expiry_reminder_days_before",
                schema: "core",
                table: "restaurant_contracts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "expiry_reminder_sent_at",
                schema: "core",
                table: "restaurant_contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE core.restaurant_contracts
                SET expiry_reminder_at = end_date - INTERVAL '1 day'
                WHERE end_date IS NOT NULL
                  AND expiry_reminder_at IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_contracts_expiry_reminder",
                schema: "core",
                table: "restaurant_contracts",
                columns: new[] { "status_id", "expiry_reminder_at", "expiry_reminder_sent_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_restaurant_contracts_expiry_reminder",
                schema: "core",
                table: "restaurant_contracts");

            migrationBuilder.DropColumn(
                name: "amount",
                schema: "core",
                table: "restaurant_contracts");

            migrationBuilder.DropColumn(
                name: "expiry_reminder_at",
                schema: "core",
                table: "restaurant_contracts");

            migrationBuilder.DropColumn(
                name: "expiry_reminder_days_before",
                schema: "core",
                table: "restaurant_contracts");

            migrationBuilder.DropColumn(
                name: "expiry_reminder_sent_at",
                schema: "core",
                table: "restaurant_contracts");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_contracts_status_id",
                schema: "core",
                table: "restaurant_contracts",
                column: "status_id");
        }
    }
}
