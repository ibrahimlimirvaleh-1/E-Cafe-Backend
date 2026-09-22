using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationStatusHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reservation_status_history",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reservation_id = table.Column<int>(type: "integer", nullable: false),
                    from_status_id = table.Column<int>(type: "integer", nullable: true),
                    to_status_id = table.Column<int>(type: "integer", nullable: false),
                    changed_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("reservation_status_history_pkey", x => x.id);
                    table.ForeignKey(
                        name: "reservation_status_history_changed_by_user_id_fkey",
                        column: x => x.changed_by_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "reservation_status_history_from_status_id_fkey",
                        column: x => x.from_status_id,
                        principalSchema: "auth",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "reservation_status_history_reservation_id_fkey",
                        column: x => x.reservation_id,
                        principalSchema: "ops",
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "reservation_status_history_to_status_id_fkey",
                        column: x => x.to_status_id,
                        principalSchema: "auth",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_status_history_changed_by_user_id",
                schema: "ops",
                table: "reservation_status_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_status_history_from_status_id",
                schema: "ops",
                table: "reservation_status_history",
                column: "from_status_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservation_status_history_to_status_id",
                schema: "ops",
                table: "reservation_status_history",
                column: "to_status_id");

            migrationBuilder.CreateIndex(
                name: "reservation_status_history_reservation_changed_at_idx",
                schema: "ops",
                table: "reservation_status_history",
                columns: new[] { "reservation_id", "changed_at" });

            migrationBuilder.Sql("""
                INSERT INTO ops.reservation_status_history
                    (reservation_id, to_status_id, changed_at, reason)
                SELECT
                    r.id,
                    r.status_id,
                    COALESCE(r."CreatedAt", now()),
                    'Mövcud rezervasiyanın ilkin statusu.'
                FROM ops.reservations AS r
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM ops.reservation_status_history AS h
                    WHERE h.reservation_id = r.id
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_status_history",
                schema: "ops");
        }
    }
}
