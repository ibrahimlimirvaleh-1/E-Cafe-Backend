using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addPermissionToOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "auth",
                table: "role_permisions",
                columns: new[] { "permission_id", "role_id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Id", "IsDeleted", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 2, 2, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 15, 2, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null },
                    { 2, 3, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, 0, false, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 2, 2 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 15, 2 });

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 2, 3 });
        }
    }
}
