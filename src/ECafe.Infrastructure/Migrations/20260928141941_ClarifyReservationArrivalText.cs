using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClarifyReservationArrivalText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 29,
                column: "label",
                value: "Müştərini masaya əyləşdir");

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 30,
                column: "label",
                value: "Müştərini masaya əyləşdir");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 29,
                column: "label",
                value: "Müştərini check-in et");

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 30,
                column: "label",
                value: "Müştərini check-in et");
        }
    }
}
