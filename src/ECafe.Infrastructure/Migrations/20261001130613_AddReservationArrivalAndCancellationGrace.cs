using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationArrivalAndCancellationGrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "cancellation_grace_deadline_at",
                schema: "ops",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "reservation_arrival_adjustments",
                schema: "ops",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reservation_id = table.Column<int>(type: "integer", nullable: false),
                    requested_arrival_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    original_no_show_deadline_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    maximum_no_show_deadline_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    proposed_no_show_deadline_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    must_vacate_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decision_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consent_token = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservation_arrival_adjustments", x => x.id);
                    table.ForeignKey(
                        name: "FK_reservation_arrival_adjustments_reservations_reservation_id",
                        column: x => x.reservation_id,
                        principalSchema: "ops",
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservation_arrival_adjustments_reservation_id",
                schema: "ops",
                table: "reservation_arrival_adjustments",
                column: "reservation_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservation_arrival_adjustments",
                schema: "ops");

            migrationBuilder.DropColumn(
                name: "cancellation_grace_deadline_at",
                schema: "ops",
                table: "reservations");
        }
    }
}
