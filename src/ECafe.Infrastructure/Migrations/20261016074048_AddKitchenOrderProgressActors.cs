using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKitchenOrderProgressActors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "accepted_by_user_id",
                schema: "ops",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "preparing_by_user_id",
                schema: "ops",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ready_by_user_id",
                schema: "ops",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_accepted_by_user_id",
                schema: "ops",
                table: "orders",
                column: "accepted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_preparing_by_user_id",
                schema: "ops",
                table: "orders",
                column: "preparing_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_ready_by_user_id",
                schema: "ops",
                table: "orders",
                column: "ready_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "orders_accepted_by_user_id_fkey",
                schema: "ops",
                table: "orders",
                column: "accepted_by_user_id",
                principalSchema: "auth",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "orders_preparing_by_user_id_fkey",
                schema: "ops",
                table: "orders",
                column: "preparing_by_user_id",
                principalSchema: "auth",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "orders_ready_by_user_id_fkey",
                schema: "ops",
                table: "orders",
                column: "ready_by_user_id",
                principalSchema: "auth",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "orders_accepted_by_user_id_fkey",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "orders_preparing_by_user_id_fkey",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "orders_ready_by_user_id_fkey",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_accepted_by_user_id",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_preparing_by_user_id",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_ready_by_user_id",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "accepted_by_user_id",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "preparing_by_user_id",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ready_by_user_id",
                schema: "ops",
                table: "orders");
        }
    }
}
