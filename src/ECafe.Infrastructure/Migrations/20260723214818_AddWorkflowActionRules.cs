using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowActionRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workflow_action_rules",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    flow_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    action_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    http_method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    endpoint_template = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    requires_confirmation = table.Column<bool>(type: "boolean", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("workflow_action_rules_pkey", x => x.id);
                    table.ForeignKey(
                        name: "workflow_action_rules_role_id_fkey",
                        column: x => x.role_id,
                        principalSchema: "auth",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "workflow_action_rules_status_id_fkey",
                        column: x => x.status_id,
                        principalSchema: "auth",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "core",
                table: "workflow_action_rules",
                columns: new[] { "id", "action_code", "endpoint_template", "flow_code", "http_method", "is_enabled", "label", "requires_confirmation", "role_id", "sort_order", "status_id" },
                values: new object[,]
                {
                    { 1, "sendForSignature", "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/send-for-signature", "contract", "POST", true, "Sahibkar təsdiqinə göndər", false, 1, 10, 6001 },
                    { 2, "terminate", "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate", "contract", "POST", true, "Müqaviləni ləğv et", true, 1, 90, 6001 },
                    { 3, "approve", "/api/v1/restaurants/{restaurantId}/contracts/{contractId}/approve", "contract", "POST", true, "Müqaviləni təsdiqlə", true, 2, 10, 6002 },
                    { 4, "terminate", "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate", "contract", "POST", true, "Müqaviləni ləğv et", true, 1, 90, 6002 },
                    { 5, "activate", "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/activate", "contract", "POST", true, "Müqaviləni aktivləşdir", false, 1, 10, 6006 },
                    { 6, "terminate", "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate", "contract", "POST", true, "Müqaviləni ləğv et", true, 1, 90, 6006 },
                    { 7, "terminate", "/api/v1/admin/restaurants/{restaurantId}/contracts/{contractId}/terminate", "contract", "POST", true, "Müqaviləni ləğv et", true, 1, 90, 6003 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_action_rules_role_id",
                schema: "core",
                table: "workflow_action_rules",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_action_rules_status_id",
                schema: "core",
                table: "workflow_action_rules",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "workflow_action_rules_flow_status_role_action_key",
                schema: "core",
                table: "workflow_action_rules",
                columns: new[] { "flow_code", "status_id", "role_id", "action_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workflow_action_rules",
                schema: "core");
        }
    }
}
