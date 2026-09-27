using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationRefundWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "requires_reason",
                schema: "core",
                table: "workflow_action_rules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "reservation_refund_status_histories",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reservation_refund_id = table.Column<int>(type: "integer", nullable: false),
                    from_status_id = table.Column<int>(type: "integer", nullable: true),
                    to_status_id = table.Column<int>(type: "integer", nullable: false),
                    changed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("reservation_refund_status_histories_pkey", x => x.id);
                    table.ForeignKey(
                        name: "reservation_refund_status_histories_changed_by_user_id_fkey",
                        column: x => x.changed_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "reservation_refund_status_histories_from_status_id_fkey",
                        column: x => x.from_status_id,
                        principalSchema: "auth",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "reservation_refund_status_histories_refund_id_fkey",
                        column: x => x.reservation_refund_id,
                        principalSchema: "billing",
                        principalTable: "reservation_refunds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "reservation_refund_status_histories_to_status_id_fkey",
                        column: x => x.to_status_id,
                        principalSchema: "auth",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 15,
                column: "requires_reason",
                value: true);

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 19,
                column: "requires_reason",
                value: true);

            migrationBuilder.InsertData(
                schema: "core",
                table: "workflow_action_rules",
                columns: new[] { "id", "action_code", "endpoint_template", "flow_code", "http_method", "is_enabled", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[,]
                {
                    { 33, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 3, 90, 1002 },
                    { 34, "cancel", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel", "reservation", "POST", true, "Rezervasiyanı ləğv et", true, 2, 90, 1002 },
                    { 35, "requestRefund", "/api/v1/public/reservations/{reservationId}/refunds", "reservation", "POST", true, "Geri ödəniş soruş", true, 5, 100, 1005 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_refund_status_histories_changed_by_user_id",
                schema: "billing",
                table: "reservation_refund_status_histories",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_refund_status_histories_from_status_id",
                schema: "billing",
                table: "reservation_refund_status_histories",
                column: "from_status_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_refund_status_histories_to_status_id",
                schema: "billing",
                table: "reservation_refund_status_histories",
                column: "to_status_id");

            migrationBuilder.CreateIndex(
                name: "reservation_refund_status_histories_refund_changed_at_idx",
                schema: "billing",
                table: "reservation_refund_status_histories",
                columns: new[] { "reservation_refund_id", "changed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_refund_status_histories",
                schema: "billing");

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

            migrationBuilder.DropColumn(
                name: "requires_reason",
                schema: "core",
                table: "workflow_action_rules");
        }
    }
}

