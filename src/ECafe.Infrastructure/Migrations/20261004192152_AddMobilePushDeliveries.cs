using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMobilePushDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mobile_push_deliveries",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    outbox_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    restaurant_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    locked_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    first_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ticket_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    sent_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mobile_push_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_mobile_push_deliveries_mobile_push_installations_installati~",
                        column: x => x.installation_id,
                        principalSchema: "core",
                        principalTable: "mobile_push_installations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_mobile_push_deliveries_outbox_events_outbox_event_id",
                        column: x => x.outbox_event_id,
                        principalSchema: "audit",
                        principalTable: "outbox_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mobile_push_deliveries_installation_id",
                schema: "core",
                table: "mobile_push_deliveries",
                column: "installation_id");

            migrationBuilder.CreateIndex(
                name: "IX_mobile_push_deliveries_outbox_event_id_installation_id",
                schema: "core",
                table: "mobile_push_deliveries",
                columns: new[] { "outbox_event_id", "installation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mobile_push_deliveries_status_next_attempt_at_locked_until",
                schema: "core",
                table: "mobile_push_deliveries",
                columns: new[] { "status", "next_attempt_at", "locked_until" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mobile_push_deliveries",
                schema: "core");
        }
    }
}
