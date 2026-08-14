using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledContractStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "auth",
                table: "statuses",
                columns: new[] { "id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "name", "status_type_id", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 6007, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, "Planlaşdırılıb", 6, null, null });

            migrationBuilder.InsertData(
                schema: "core",
                table: "workflow_action_rules",
                columns: new[] { "id", "action_code", "endpoint_template", "flow_code", "http_method", "is_enabled", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[] { 31, "terminate", "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate", "contract", "POST", true, "Müqaviləni ləğv et", true, 1, 90, 6007 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "core",
                table: "workflow_action_rules",
                keyColumn: "id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                schema: "auth",
                table: "statuses",
                keyColumn: "id",
                keyValue: 6007);
        }
    }
}
