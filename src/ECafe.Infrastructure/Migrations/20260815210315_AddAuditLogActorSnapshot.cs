using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogActorSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorEmail",
                schema: "audit",
                table: "audit_logs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorFullName",
                schema: "audit",
                table: "audit_logs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActorRoleId",
                schema: "audit",
                table: "audit_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorRoleName",
                schema: "audit",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_RestaurantId_Action_OccurredAt",
                schema: "audit",
                table: "audit_logs",
                columns: new[] { "RestaurantId", "Action", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_RestaurantId_UserId_OccurredAt",
                schema: "audit",
                table: "audit_logs",
                columns: new[] { "RestaurantId", "UserId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_logs_RestaurantId_Action_OccurredAt",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_RestaurantId_UserId_OccurredAt",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "ActorEmail",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "ActorFullName",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "ActorRoleId",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "ActorRoleName",
                schema: "audit",
                table: "audit_logs");
        }
    }
}
