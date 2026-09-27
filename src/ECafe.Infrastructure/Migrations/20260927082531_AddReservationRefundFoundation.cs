using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationRefundFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "payment_instruction_id",
                schema: "billing",
                table: "reservation_payment_proofs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "reservation_refunds",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reservation_id = table.Column<int>(type: "integer", nullable: false),
                    source_payment_proof_id = table.Column<int>(type: "integer", nullable: true),
                    status_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    initiated_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    approved_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    refunded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    eligibility_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    cancellation_reason_snapshot = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("reservation_refunds_pkey", x => x.id);
                    table.ForeignKey(
                        name: "reservation_refunds_approved_by_user_id_fkey",
                        column: x => x.approved_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "reservation_refunds_initiated_by_user_id_fkey",
                        column: x => x.initiated_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "reservation_refunds_reservation_id_fkey",
                        column: x => x.reservation_id,
                        principalSchema: "ops",
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "reservation_refunds_source_payment_proof_id_fkey",
                        column: x => x.source_payment_proof_id,
                        principalSchema: "billing",
                        principalTable: "reservation_payment_proofs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "reservation_refunds_status_id_fkey",
                        column: x => x.status_id,
                        principalSchema: "auth",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reservation_refund_transfers",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reservation_refund_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    transfer_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    proof_file_id = table.Column<int>(type: "integer", nullable: true),
                    submitted_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    customer_confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    disputed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dispute_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("reservation_refund_transfers_pkey", x => x.id);
                    table.ForeignKey(
                        name: "reservation_refund_transfers_proof_file_id_fkey",
                        column: x => x.proof_file_id,
                        principalSchema: "common",
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "reservation_refund_transfers_refund_id_fkey",
                        column: x => x.reservation_refund_id,
                        principalSchema: "billing",
                        principalTable: "reservation_refunds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "reservation_refund_transfers_submitted_by_user_id_fkey",
                        column: x => x.submitted_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "auth",
                table: "status_types",
                columns: new[] { "id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 8, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Geri ödəniş statusları", null, null });

            migrationBuilder.InsertData(
                schema: "auth",
                table: "statuses",
                columns: new[] { "id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "status_type_id", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 8001, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Geri ödəniş sorğusu gözləyir", 8, null, null },
                    { 8002, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Geri ödəniş məlumatları gözlənilir", 8, null, null },
                    { 8003, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Geri ödəniş üçün hazırdır", 8, null, null },
                    { 8004, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Geri ödəniş emal edilir", 8, null, null },
                    { 8005, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Geri ödəniş tamamlandı", 8, null, null },
                    { 8006, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Geri ödənişlə bağlı etiraz var", 8, null, null },
                    { 8007, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Geri ödəniş uğursuz oldu", 8, null, null },
                    { 8008, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Geri ödəniş sorğusu rədd edildi", 8, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "reservation_payment_proofs_payment_instruction_id_idx",
                schema: "billing",
                table: "reservation_payment_proofs",
                column: "payment_instruction_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_refund_transfers_submitted_by_user_id",
                schema: "billing",
                table: "reservation_refund_transfers",
                column: "submitted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "reservation_refund_transfers_proof_file_id_idx",
                schema: "billing",
                table: "reservation_refund_transfers",
                column: "proof_file_id");

            migrationBuilder.CreateIndex(
                name: "reservation_refund_transfers_refund_submitted_at_idx",
                schema: "billing",
                table: "reservation_refund_transfers",
                columns: new[] { "reservation_refund_id", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_refunds_approved_by_user_id",
                schema: "billing",
                table: "reservation_refunds",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_refunds_initiated_by_user_id",
                schema: "billing",
                table: "reservation_refunds",
                column: "initiated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_refunds_source_payment_proof_id",
                schema: "billing",
                table: "reservation_refunds",
                column: "source_payment_proof_id");

            migrationBuilder.CreateIndex(
                name: "reservation_refunds_reservation_id_key",
                schema: "billing",
                table: "reservation_refunds",
                column: "reservation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "reservation_refunds_status_requested_at_idx",
                schema: "billing",
                table: "reservation_refunds",
                columns: new[] { "status_id", "requested_at" });

            migrationBuilder.AddForeignKey(
                name: "reservation_payment_proofs_payment_instruction_id_fkey",
                schema: "billing",
                table: "reservation_payment_proofs",
                column: "payment_instruction_id",
                principalSchema: "billing",
                principalTable: "reservation_payment_instructions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "reservation_payment_proofs_payment_instruction_id_fkey",
                schema: "billing",
                table: "reservation_payment_proofs");

            migrationBuilder.DropTable(
                name: "reservation_refund_transfers",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "reservation_refunds",
                schema: "billing");

            migrationBuilder.DropIndex(
                name: "reservation_payment_proofs_payment_instruction_id_idx",
                schema: "billing",
                table: "reservation_payment_proofs");

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 8001);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 8002);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 8003);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 8004);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 8005);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 8006);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 8007);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 8008);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "status_types",
                keyColumn: "id",
                keyValue: 8);

            migrationBuilder.DropColumn(
                name: "payment_instruction_id",
                schema: "billing",
                table: "reservation_payment_proofs");
        }
    }
}
