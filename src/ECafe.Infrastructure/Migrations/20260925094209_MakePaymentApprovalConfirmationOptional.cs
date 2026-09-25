using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakePaymentApprovalConfirmationOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 14,
                column: "requires_confirmation",
                value: false);

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 18,
                column: "requires_confirmation",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 14,
                column: "requires_confirmation",
                value: true);

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 18,
                column: "requires_confirmation",
                value: true);
        }
    }
}
