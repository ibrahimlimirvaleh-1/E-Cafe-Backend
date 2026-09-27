using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationRefundPayoutDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reservation_refund_payout_details",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reservation_refund_id = table.Column<int>(type: "integer", nullable: false),
                    encrypted_details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    masked_details = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    submitted_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("reservation_refund_payout_details_pkey", x => x.id);
                    table.ForeignKey(
                        name: "reservation_refund_payout_details_refund_id_fkey",
                        column: x => x.reservation_refund_id,
                        principalSchema: "billing",
                        principalTable: "reservation_refunds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "reservation_refund_payout_details_submitted_by_user_id_fkey",
                        column: x => x.submitted_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "core",
                table: "workflow_action_rules",
                columns: new[] { "id", "action_code", "endpoint_template", "flow_code", "http_method", "is_enabled", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[] { 36, "submitPayoutDetails", "/api/v1/public/refunds/{refundId}/payout-details", "refund", "POST", true, "Geri ödəniş məlumatını göndər", false, 5, 10, 8002 });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_refund_payout_details_submitted_by_user_id",
                schema: "billing",
                table: "reservation_refund_payout_details",
                column: "submitted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "reservation_refund_payout_details_refund_id_key",
                schema: "billing",
                table: "reservation_refund_payout_details",
                column: "reservation_refund_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_refund_payout_details",
                schema: "billing");

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 36);
        }
    }
}
