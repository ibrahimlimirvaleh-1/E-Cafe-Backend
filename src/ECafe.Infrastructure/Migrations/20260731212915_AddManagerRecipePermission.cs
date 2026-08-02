using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerRecipePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "inventory",
                table: "inventory_movement_types",
                keyColumn: "id",
                keyValue: 1,
                column: "name",
                value: "Alış");

            migrationBuilder.UpdateData(
                schema: "inventory",
                table: "inventory_movement_types",
                keyColumn: "id",
                keyValue: 2,
                column: "name",
                value: "Manual artım");

            migrationBuilder.UpdateData(
                schema: "inventory",
                table: "inventory_movement_types",
                keyColumn: "id",
                keyValue: 3,
                column: "name",
                value: "Manual azalma");

            migrationBuilder.UpdateData(
                schema: "inventory",
                table: "inventory_movement_types",
                keyColumn: "id",
                keyValue: 4,
                column: "name",
                value: "Sifariş sərfiyyatı");

            migrationBuilder.UpdateData(
                schema: "inventory",
                table: "inventory_movement_types",
                keyColumn: "id",
                keyValue: 5,
                column: "name",
                value: "İtki");

            migrationBuilder.UpdateData(
                schema: "inventory",
                table: "inventory_movement_types",
                keyColumn: "id",
                keyValue: 6,
                column: "name",
                value: "Stoka qaytarma");

            migrationBuilder.UpdateData(
                schema: "inventory",
                table: "inventory_movement_types",
                keyColumn: "id",
                keyValue: 7,
                column: "name",
                value: "Düzəliş");

            migrationBuilder.InsertData(
                schema: "auth",
                table: "role_permisions",
                columns: new[] { "permission_id", "role_id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Id", "IsDeleted", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 25, 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 25, 3 });

            migrationBuilder.UpdateData(
                schema: "inventory",
                table: "inventory_movement_types",
                keyColumn: "id",
                keyValue: 1,
                column: "name",
                value: "Purchase");

            migrationBuilder.UpdateData(
                schema: "inventory",
                table: "inventory_movement_types",
                keyColumn: "id",
                keyValue: 2,
                column: "name",
                value: "Manual increase");

            migrationBuilder.UpdateData(
                schema: "inventory",
                table: "inventory_movement_types",
                keyColumn: "id",
                keyValue: 3,
                column: "name",
                value: "Manual decrease");

            migrationBuilder.UpdateData(
                schema: "inventory",
                table: "inventory_movement_types",
                keyColumn: "id",
                keyValue: 4,
                column: "name",
                value: "Order consumption");

            migrationBuilder.UpdateData(
                schema: "inventory",
                table: "inventory_movement_types",
                keyColumn: "id",
                keyValue: 5,
                column: "name",
                value: "Waste");

            migrationBuilder.UpdateData(
                schema: "inventory",
                table: "inventory_movement_types",
                keyColumn: "id",
                keyValue: 6,
                column: "name",
                value: "Stock return");

            migrationBuilder.UpdateData(
                schema: "inventory",
                table: "inventory_movement_types",
                keyColumn: "id",
                keyValue: 7,
                column: "name",
                value: "Correction");
        }
    }
}
