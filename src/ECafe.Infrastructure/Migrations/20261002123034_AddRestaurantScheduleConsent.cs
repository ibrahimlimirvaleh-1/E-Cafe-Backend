using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantScheduleConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restaurant_schedule_changes",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RestaurantId = table.Column<int>(type: "integer", nullable: false),
                    ProposedHoursJson = table.Column<string>(type: "jsonb", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Token = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_restaurant_schedule_changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restaurant_schedule_changes_restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalSchema: "core",
                        principalTable: "restaurants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_schedule_consents",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScheduleChangeId = table.Column<int>(type: "integer", nullable: false),
                    ReservationId = table.Column<int>(type: "integer", nullable: true),
                    TableSessionId = table.Column<int>(type: "integer", nullable: true),
                    ProposedVacateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespondedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ResponseNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_restaurant_schedule_consents", x => x.Id);
                    table.CheckConstraint("ck_schedule_consent_target", "(\"ReservationId\" IS NULL) <> (\"TableSessionId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_restaurant_schedule_consents_reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalSchema: "ops",
                        principalTable: "reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_restaurant_schedule_consents_restaurant_schedule_changes_Sc~",
                        column: x => x.ScheduleChangeId,
                        principalSchema: "ops",
                        principalTable: "restaurant_schedule_changes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_restaurant_schedule_consents_table_sessions_TableSessionId",
                        column: x => x.TableSessionId,
                        principalSchema: "ops",
                        principalTable: "table_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_schedule_changes_RestaurantId",
                schema: "ops",
                table: "restaurant_schedule_changes",
                column: "RestaurantId",
                unique: true,
                filter: "\"State\" = 0 AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_schedule_consents_ReservationId",
                schema: "ops",
                table: "restaurant_schedule_consents",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_schedule_consents_ScheduleChangeId_ReservationId",
                schema: "ops",
                table: "restaurant_schedule_consents",
                columns: new[] { "ScheduleChangeId", "ReservationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_schedule_consents_ScheduleChangeId_TableSessionId",
                schema: "ops",
                table: "restaurant_schedule_consents",
                columns: new[] { "ScheduleChangeId", "TableSessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_schedule_consents_TableSessionId",
                schema: "ops",
                table: "restaurant_schedule_consents",
                column: "TableSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_schedule_consents",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "restaurant_schedule_changes",
                schema: "ops");
        }
    }
}
