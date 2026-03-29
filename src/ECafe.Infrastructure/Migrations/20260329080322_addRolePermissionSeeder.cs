using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addRolePermissionSeeder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 2,
                column: "name",
                value: "İşçiləri idarə etmək");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 3,
                column: "name",
                value: "Rolları təyin etmək");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 4,
                column: "name",
                value: "Restoranları idarə etmək");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 5,
                column: "name",
                value: "Kateqoriyaları və məhsulları idarə etmək");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 6,
                column: "name",
                value: "Stolları idarə etmək");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 7,
                column: "name",
                value: "Rezervasiyaları idarə etmək");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 8,
                column: "name",
                value: "Təyin olunmuş rezervasiyalara baxmaq");

            migrationBuilder.InsertData(
                schema: "auth",
                table: "permissions",
                columns: new[] { "id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 9, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Sifarişləri idarə etmək", null, null },
                    { 10, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Ödənişləri idarə etmək", null, null },
                    { 11, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Rəyləri idarə etmək", null, null },
                    { 12, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Balansa nəzarət etmək", null, null },
                    { 13, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Öz balansına baxmaq", null, null },
                    { 14, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Çıxarış sorğularını idarə etmək", null, null },
                    { 15, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Hesabatlara baxmaq", null, null },
                    { 16, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Dashboard-a baxmaq", null, null },
                    { 17, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Audit qeydlərinə baxmaq", null, null }
                });

            migrationBuilder.InsertData(
                schema: "auth",
                table: "role_permisions",
                columns: new[] { "permission_id", "role_id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Id", "IsDeleted", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 4, 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 5, 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 6, 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 7, 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 5, 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 6, 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 7, 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null }
                });

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "roles",
                keyColumn: "id",
                keyValue: 1,
                column: "name",
                value: "Platforma super administratoru");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "roles",
                keyColumn: "id",
                keyValue: 2,
                column: "name",
                value: "Sahibkar");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "roles",
                keyColumn: "id",
                keyValue: 3,
                column: "name",
                value: "Restoran meneceri");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "roles",
                keyColumn: "id",
                keyValue: 4,
                column: "name",
                value: "Ofisiant");

            migrationBuilder.InsertData(
                schema: "auth",
                table: "roles",
                columns: new[] { "id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 5, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Müştəri", null, null });

            migrationBuilder.InsertData(
                schema: "auth",
                table: "role_permisions",
                columns: new[] { "permission_id", "role_id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Id", "IsDeleted", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 9, 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 10, 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 15, 1, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 9, 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 10, 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 15, 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 1, 1 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 4, 1 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 5, 1 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 6, 1 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 7, 1 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 9, 1 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 10, 1 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 15, 1 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 5, 3 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 6, 3 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 7, 3 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 9, 3 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 10, 3 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 15, 3 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "roles",
                keyColumn: "id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 15);

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 2,
                column: "name",
                value: "Restoranları idarə etmək");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 3,
                column: "name",
                value: "Kateqoriya və məhsulları idarə etmək");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 4,
                column: "name",
                value: "Stolları idarə etmək");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 5,
                column: "name",
                value: "Rezervasiyaları idarə etmək");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 6,
                column: "name",
                value: "Sifarişləri idarə etmək");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 7,
                column: "name",
                value: "Ödənişləri idarə etmək");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "permissions",
                keyColumn: "id",
                keyValue: 8,
                column: "name",
                value: "Hesabatlara baxmaq");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "roles",
                keyColumn: "id",
                keyValue: 1,
                column: "name",
                value: "Sistem administratoru");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "roles",
                keyColumn: "id",
                keyValue: 2,
                column: "name",
                value: "Restoran meneceri");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "roles",
                keyColumn: "id",
                keyValue: 3,
                column: "name",
                value: "Ofisiant");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "roles",
                keyColumn: "id",
                keyValue: 4,
                column: "name",
                value: "Müştəri");
        }
    }
}
