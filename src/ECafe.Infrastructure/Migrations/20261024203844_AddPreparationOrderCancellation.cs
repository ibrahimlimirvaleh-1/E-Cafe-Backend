using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPreparationOrderCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                schema: "ops",
                table: "orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                schema: "ops",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cancelled_by_user_id",
                schema: "ops",
                table: "orders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_cancelled_by_user_id",
                schema: "ops",
                table: "orders",
                column: "cancelled_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "orders_cancelled_by_user_id_fkey",
                schema: "ops",
                table: "orders",
                column: "cancelled_by_user_id",
                principalSchema: "auth",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "orders_cancelled_by_user_id_fkey",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_cancelled_by_user_id",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "cancelled_by_user_id",
                schema: "ops",
                table: "orders");
        }
    }
}
