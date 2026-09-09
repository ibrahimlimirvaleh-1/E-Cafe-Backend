using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptReservationDatabaseSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reservations_restaurant_id",
                schema: "ops",
                table: "reservations");

            migrationBuilder.AddColumn<string>(
                name: "cancel_reason",
                schema: "ops",
                table: "reservations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "confirmed_at",
                schema: "ops",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "hold_expires_at",
                schema: "ops",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "payment_submitted_at",
                schema: "ops",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reject_reason",
                schema: "ops",
                table: "reservations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "rejected_at",
                schema: "ops",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_snapshot",
                schema: "ops",
                table: "order_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "unit_price_snapshot",
                schema: "ops",
                table: "order_items",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "reservation_payment_proofs",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reservation_id = table.Column<int>(type: "integer", nullable: false),
                    file_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status_id = table.Column<int>(type: "integer", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    reviewed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reject_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("reservation_payment_proofs_pkey", x => x.id);
                    table.ForeignKey(
                        name: "reservation_payment_proofs_file_id_fkey",
                        column: x => x.file_id,
                        principalSchema: "common",
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "reservation_payment_proofs_reservation_id_fkey",
                        column: x => x.reservation_id,
                        principalSchema: "ops",
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "reservation_payment_proofs_reviewed_by_user_id_fkey",
                        column: x => x.reviewed_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "reservation_payment_proofs_status_id_fkey",
                        column: x => x.status_id,
                        principalSchema: "auth",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_payment_instructions",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    restaurant_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("restaurant_payment_instructions_pkey", x => x.id);
                    table.ForeignKey(
                        name: "restaurant_payment_instructions_restaurant_id_fkey",
                        column: x => x.restaurant_id,
                        principalSchema: "core",
                        principalTable: "restaurants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 1001,
                column: "name",
                value: "Ödəniş gözlənilir");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 1002,
                column: "name",
                value: "Rezervasiya təsdiqlənib");

            migrationBuilder.InsertData(
                schema: "auth",
                table: "statuses",
                columns: new[] { "id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "status_type_id", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1008, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Ödəniş çeki göndərilib", 1, null, null },
                    { 1009, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Rezervasiya rədd edilib", 1, null, null },
                    { 3006, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Ödəniş sübutu göndərilib", 3, null, null },
                    { 3007, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Ödəniş rədd edilib", 3, null, null }
                });

            migrationBuilder.InsertData(
                schema: "core",
                table: "workflow_action_rules",
                columns: new[] { "id", "action_code", "endpoint_template", "flow_code", "http_method", "is_enabled", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[,]
                {
                    { 32, "submitPaymentProof", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs", "reservation", "POST", true, "Ödəniş çekini göndər", false, 5, 10, 1001 },
                    { 33, "approvePaymentProof", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs/approve", "reservation", "POST", true, "Ödənişi təsdiqlə", true, 3, 10, 1008 },
                    { 34, "rejectPaymentProof", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment-proofs/reject", "reservation", "POST", true, "Ödənişi rədd et", true, 3, 20, 1008 },
                    { 35, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 3, 90, 1008 }
                });

            migrationBuilder.CreateIndex(
                name: "reservations_availability_lookup_idx",
                schema: "ops",
                table: "reservations",
                columns: new[] { "restaurant_id", "table_id", "status_id", "reserved_from", "reserved_to" });

            migrationBuilder.CreateIndex(
                name: "reservations_hold_expires_at_idx",
                schema: "ops",
                table: "reservations",
                column: "hold_expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_payment_proofs_status_id",
                schema: "billing",
                table: "reservation_payment_proofs",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "reservation_payment_proofs_file_id_idx",
                schema: "billing",
                table: "reservation_payment_proofs",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "reservation_payment_proofs_reservation_status_idx",
                schema: "billing",
                table: "reservation_payment_proofs",
                columns: new[] { "reservation_id", "status_id" });

            migrationBuilder.CreateIndex(
                name: "reservation_payment_proofs_reviewed_by_user_id_idx",
                schema: "billing",
                table: "reservation_payment_proofs",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "restaurant_payment_instructions_restaurant_active_idx",
                schema: "billing",
                table: "restaurant_payment_instructions",
                columns: new[] { "restaurant_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_payment_proofs",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "restaurant_payment_instructions",
                schema: "billing");

            migrationBuilder.DropIndex(
                name: "reservations_availability_lookup_idx",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "reservations_hold_expires_at_idx",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 1009);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 3006);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 3007);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 1008);

            migrationBuilder.DropColumn(
                name: "cancel_reason",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "confirmed_at",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "hold_expires_at",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "payment_submitted_at",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "reject_reason",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "rejected_at",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "name_snapshot",
                schema: "ops",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "unit_price_snapshot",
                schema: "ops",
                table: "order_items");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 1001,
                column: "name",
                value: "Depozit ödənişi gözlənilir");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 1002,
                column: "name",
                value: "Depozit ödənilib, stol rezerv olunub");

            migrationBuilder.CreateIndex(
                name: "IX_reservations_restaurant_id",
                schema: "ops",
                table: "reservations",
                column: "restaurant_id");
        }
    }
}
