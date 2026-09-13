using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusTableAvailabilityMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "blocks_table_availability",
                schema: "auth",
                table: "statuses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 1001,
                column: "blocks_table_availability",
                value: true);

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 1002,
                column: "blocks_table_availability",
                value: true);

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 1003,
                column: "blocks_table_availability",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "blocks_table_availability",
                schema: "auth",
                table: "statuses");
        }
    }
}
