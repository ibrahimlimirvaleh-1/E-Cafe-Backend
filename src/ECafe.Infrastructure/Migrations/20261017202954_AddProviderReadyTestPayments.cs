using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderReadyTestPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "provider_name",
                schema: "billing",
                table: "reservation_refunds",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_ref",
                schema: "billing",
                table: "reservation_refunds",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "provider_status",
                schema: "billing",
                table: "reservation_refunds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "source_payment_id",
                schema: "billing",
                table: "reservation_refunds",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "checkout_expires_at",
                schema: "billing",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "checkout_key",
                schema: "billing",
                table: "payments",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "checkout_url",
                schema: "billing",
                table: "payments",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                schema: "billing",
                table: "payments",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "AZN");

            migrationBuilder.AddColumn<bool>(
                name: "is_test",
                schema: "billing",
                table: "payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "provider_name",
                schema: "billing",
                table: "payments",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "provider_status",
                schema: "billing",
                table: "payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "requires_refund",
                schema: "billing",
                table: "payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_reservation_refunds_source_payment_id",
                schema: "billing",
                table: "reservation_refunds",
                column: "source_payment_id",
                unique: true,
                filter: "source_payment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "payments_customer_checkout_key",
                schema: "billing",
                table: "payments",
                columns: new[] { "created_by_user_id", "checkout_key" },
                unique: true,
                filter: "checkout_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "payments_pending_reservation_key",
                schema: "billing",
                table: "payments",
                column: "reservation_id",
                unique: true,
                filter: "reservation_id IS NOT NULL AND provider_name IS NOT NULL AND payment_status_id = 3001 AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "payments_provider_reference_key",
                schema: "billing",
                table: "payments",
                columns: new[] { "provider_name", "provider_ref" },
                unique: true,
                filter: "provider_name IS NOT NULL AND provider_ref IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_reservation_refunds_payments_source_payment_id",
                schema: "billing",
                table: "reservation_refunds",
                column: "source_payment_id",
                principalSchema: "billing",
                principalTable: "payments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_reservation_refunds_payments_source_payment_id",
                schema: "billing",
                table: "reservation_refunds");

            migrationBuilder.DropIndex(
                name: "IX_reservation_refunds_source_payment_id",
                schema: "billing",
                table: "reservation_refunds");

            migrationBuilder.DropIndex(
                name: "payments_customer_checkout_key",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "payments_pending_reservation_key",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "payments_provider_reference_key",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "provider_name",
                schema: "billing",
                table: "reservation_refunds");

            migrationBuilder.DropColumn(
                name: "provider_ref",
                schema: "billing",
                table: "reservation_refunds");

            migrationBuilder.DropColumn(
                name: "provider_status",
                schema: "billing",
                table: "reservation_refunds");

            migrationBuilder.DropColumn(
                name: "source_payment_id",
                schema: "billing",
                table: "reservation_refunds");

            migrationBuilder.DropColumn(
                name: "checkout_expires_at",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "checkout_key",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "checkout_url",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "currency_code",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "is_test",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "provider_name",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "provider_status",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "requires_refund",
                schema: "billing",
                table: "payments");

        }
    }
}
