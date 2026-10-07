using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationPhysicalArrival : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "arrived_at",
                schema: "ops",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "arrived_by_user_id",
                schema: "ops",
                table: "reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.InsertData(
                schema: "auth",
                table: "permissions",
                columns: new[] { "id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 27, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Rezervasiya müştərisinin gəlişini qeyd etmək", null, null },
                    { 28, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Rezervasiya müştərisini masaya əyləşdirmək", null, null }
                });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 29,
                columns: new[] { "action_code", "endpoint_template", "label", "requires_confirmation" },
                values: new object[] { "markArrived", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/arrival", "Müştəri gəlib", false });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 30,
                columns: new[] { "label", "requires_confirmation", "role_id", "sort_order" },
                values: new object[] { "Masaya əyləşdir", false, 4, 20 });

            migrationBuilder.InsertData(
                schema: "core",
                table: "workflow_action_rules",
                columns: new[] { "id", "action_code", "endpoint_template", "flow_code", "http_method", "is_enabled", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[,]
                {
                    { 51, "markArrived", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/arrival", "reservation", "POST", true, "Müştəri gəlib", false, 2, 10, 1002 },
                    { 52, "markArrived", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/arrival", "reservation", "POST", true, "Müştəri gəlib", false, 4, 10, 1002 }
                });

            migrationBuilder.InsertData(
                schema: "auth",
                table: "role_permisions",
                columns: new[] { "permission_id", "role_id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Id", "IsDeleted", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 27, 2, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 27, 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 27, 4, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 28, 4, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_arrived_by_user_id",
                schema: "ops",
                table: "reservations",
                column: "arrived_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "reservations_arrived_by_user_id_fkey",
                schema: "ops",
                table: "reservations",
                column: "arrived_by_user_id",
                principalSchema: "auth",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "reservations_arrived_by_user_id_fkey",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropIndex(
                name: "IX_reservations_arrived_by_user_id",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 27, 2 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 27, 3 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 27, 4 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 28, 4 });

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 28);

            migrationBuilder.DropColumn(
                name: "arrived_at",
                schema: "ops",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "arrived_by_user_id",
                schema: "ops",
                table: "reservations");

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 29,
                columns: new[] { "action_code", "endpoint_template", "label", "requires_confirmation" },
                values: new object[] { "checkIn", "/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/check-in", "Müştərini masaya əyləşdir", true });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 30,
                columns: new[] { "label", "requires_confirmation", "role_id", "sort_order" },
                values: new object[] { "Müştərini masaya əyləşdir", true, 2, 10 });
        }
    }
}
