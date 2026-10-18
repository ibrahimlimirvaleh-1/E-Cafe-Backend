using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTableSessionBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bill_fingerprint",
                schema: "billing",
                table: "payments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "table_session_id",
                schema: "billing",
                table: "payments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_table_session_id",
                schema: "billing",
                table: "payments",
                column: "table_session_id");

            migrationBuilder.CreateIndex(
                name: "payments_pending_session_key",
                schema: "billing",
                table: "payments",
                column: "table_session_id",
                unique: true,
                filter: "table_session_id IS NOT NULL AND payment_status_id = 3001 AND NOT \"IsDeleted\"");

            migrationBuilder.AddCheckConstraint(
                name: "payments_session_bill_link_check",
                schema: "billing",
                table: "payments",
                sql: "(table_session_id IS NULL AND bill_fingerprint IS NULL) OR (table_session_id IS NOT NULL AND reservation_id IS NULL AND order_id IS NULL AND bill_fingerprint IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_payments_table_sessions_table_session_id",
                schema: "billing",
                table: "payments",
                column: "table_session_id",
                principalSchema: "ops",
                principalTable: "table_sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payments_table_sessions_table_session_id",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_table_session_id",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "payments_pending_session_key",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropCheckConstraint(
                name: "payments_session_bill_link_check",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "bill_fingerprint",
                schema: "billing",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "table_session_id",
                schema: "billing",
                table: "payments");
        }
    }
}
