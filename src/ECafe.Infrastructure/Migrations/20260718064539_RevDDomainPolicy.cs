using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RevDDomainPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "service_fee_percent",
                schema: "auth",
                table: "user_restaurants",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "staff_settlement_period",
                schema: "core",
                table: "restaurants",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.InsertData(
                schema: "auth",
                table: "permissions",
                columns: new[] { "id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 19, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Mətbəx sifariş statuslarını idarə etmək", null, null });

            migrationBuilder.InsertData(
                schema: "auth",
                table: "roles",
                columns: new[] { "id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 6, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Mətbəx", null, null });


            migrationBuilder.Sql(
                """
                UPDATE ops.orders
                SET status_id = CASE status_id
                    WHEN 2005 THEN 2007
                    WHEN 2004 THEN 2006
                    WHEN 2003 THEN 2005
                    WHEN 2002 THEN 2003
                    ELSE status_id
                END
                WHERE status_id IN (2002, 2003, 2004, 2005);
                """);

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 4001,
                column: "IsDeleted",
                value: true);

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 4002,
                column: "IsDeleted",
                value: true);

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 2001,
                column: "name",
                value: "Sifariş yaradılıb");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 2002,
                column: "name",
                value: "Sifariş mətbəx tərəfindən qəbul edilib");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 2003,
                column: "name",
                value: "Sifariş hazırlanır");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 2004,
                column: "name",
                value: "Sifariş hazırdır");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 2005,
                column: "name",
                value: "Sifariş təqdim olunub");

            migrationBuilder.InsertData(
                schema: "auth",
                table: "statuses",
                columns: new[] { "id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "status_type_id", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 2006, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Sifariş online ödənişlə bağlanıb", 2, null, null },
                    { 2007, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Sifariş ləğv edilib", 2, null, null }
                });

            migrationBuilder.InsertData(
                schema: "auth",
                table: "role_permisions",
                columns: new[] { "permission_id", "role_id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Id", "IsDeleted", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 18, 6, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 19, 6, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 18, 6 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 19, 6 });

            migrationBuilder.Sql(
                """
                UPDATE ops.orders
                SET status_id = CASE status_id
                    WHEN 2007 THEN 2005
                    WHEN 2006 THEN 2004
                    WHEN 2005 THEN 2003
                    WHEN 2004 THEN 2002
                    WHEN 2003 THEN 2002
                    ELSE status_id
                END
                WHERE status_id IN (2003, 2004, 2005, 2006, 2007);
                """);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 2006);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 2007);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "roles",
                keyColumn: "id",
                keyValue: 6);

            migrationBuilder.DropColumn(
                name: "service_fee_percent",
                schema: "auth",
                table: "user_restaurants");

            migrationBuilder.DropColumn(
                name: "staff_settlement_period",
                schema: "core",
                table: "restaurants");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 4001,
                column: "IsDeleted",
                value: false);

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 4002,
                column: "IsDeleted",
                value: false);

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 2001,
                column: "name",
                value: "Sifariş açılıb");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 2002,
                column: "name",
                value: "Sifariş hazırlanır");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 2003,
                column: "name",
                value: "Sifariş təqdim olunub");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 2004,
                column: "name",
                value: "Sifariş bağlanıb");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 2005,
                column: "name",
                value: "Sifariş ləğv edilib");
        }
    }
}
