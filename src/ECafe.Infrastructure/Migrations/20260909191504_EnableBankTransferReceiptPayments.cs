using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnableBankTransferReceiptPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "auth",
                table: "statuses",
                columns: new[] { "id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "status_type_id", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 4004, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Köçürmə ilə ödəniş", 4, null, null });

            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 25,
                column: "is_enabled",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 25,
                column: "is_enabled",
                value: true);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 4004);
        }
    }
}
