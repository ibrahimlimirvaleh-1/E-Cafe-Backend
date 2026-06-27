using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addStatusToItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "status_id",
                schema: "catalog",
                table: "items",
                type: "integer",
                nullable: false,
                defaultValue: 5001);

            migrationBuilder.CreateIndex(
                name: "IX_items_status_id",
                schema: "catalog",
                table: "items",
                column: "status_id");

            migrationBuilder.AddForeignKey(
                name: "items_status_id_fkey",
                schema: "catalog",
                table: "items",
                column: "status_id",
                principalSchema: "auth",
                principalTable: "statuses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "items_status_id_fkey",
                schema: "catalog",
                table: "items");

            migrationBuilder.DropIndex(
                name: "IX_items_status_id",
                schema: "catalog",
                table: "items");

            migrationBuilder.DropColumn(
                name: "status_id",
                schema: "catalog",
                table: "items");
        }
    }
}
