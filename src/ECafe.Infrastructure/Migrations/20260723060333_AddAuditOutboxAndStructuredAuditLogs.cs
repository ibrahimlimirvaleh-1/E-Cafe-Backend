using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditOutboxAndStructuredAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Action",
                schema: "audit",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                schema: "audit",
                table: "audit_logs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityDisplayName",
                schema: "audit",
                table: "audit_logs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EventId",
                schema: "audit",
                table: "audit_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                schema: "audit",
                table: "audit_logs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurredAt",
                schema: "audit",
                table: "audit_logs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                schema: "audit",
                table: "audit_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "outbox_events",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AggregateId = table.Column<long>(type: "bigint", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EntityName_EntityId",
                schema: "audit",
                table: "audit_logs",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_EventId",
                schema: "audit",
                table: "audit_logs",
                column: "EventId",
                unique: true,
                filter: "\"EventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_RestaurantId_OccurredAt",
                schema: "audit",
                table: "audit_logs",
                columns: new[] { "RestaurantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_events_AggregateType_AggregateId",
                schema: "audit",
                table: "outbox_events",
                columns: new[] { "AggregateType", "AggregateId" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_events_ProcessedAt_LockedUntil_OccurredAt",
                schema: "audit",
                table: "outbox_events",
                columns: new[] { "ProcessedAt", "LockedUntil", "OccurredAt" });

            migrationBuilder.Sql("""
                UPDATE audit.audit_logs
                SET "RestaurantId" = "EntityId"::integer,
                    "OccurredAt" = "CreatedAt"
                WHERE "RestaurantId" IS NULL
                  AND "EntityName" = 'Restaurant'
                  AND "EntityId" <= 2147483647;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_events",
                schema: "audit");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_EntityName_EntityId",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_EventId",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_audit_logs_RestaurantId_OccurredAt",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "EntityDisplayName",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "EventId",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "Metadata",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "OccurredAt",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                schema: "audit",
                table: "audit_logs");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                schema: "audit",
                table: "audit_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }
    }
}
