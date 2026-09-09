using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantWorkingHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "time_zone",
                schema: "core",
                table: "restaurants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Asia/Baku");

            migrationBuilder.CreateTable(
                name: "restaurant_working_hours",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    restaurant_id = table.Column<int>(type: "integer", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    opens_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    closes_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                    table.PrimaryKey("restaurant_working_hours_pkey", x => x.id);
                    table.CheckConstraint("ck_restaurant_working_hours_day_of_week_range", "day_of_week >= 0 AND day_of_week <= 6");
                    table.CheckConstraint("ck_restaurant_working_hours_open_closed_not_equal", "is_closed = true OR opens_at <> closes_at");
                    table.ForeignKey(
                        name: "restaurant_working_hours_restaurant_id_fkey",
                        column: x => x.restaurant_id,
                        principalSchema: "core",
                        principalTable: "restaurants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO core.restaurant_working_hours (
                    restaurant_id,
                    day_of_week,
                    opens_at,
                    closes_at,
                    is_closed,
                    "CreatedAt",
                    "CreatedBy",
                    "IsDeleted"
                )
                SELECT
                    r.id,
                    days.day_of_week,
                    TIME '09:00',
                    TIME '00:00',
                    false,
                    now(),
                    'migration',
                    false
                FROM core.restaurants r
                CROSS JOIN generate_series(0, 6) AS days(day_of_week)
                WHERE r."IsDeleted" = false;
                """);

            migrationBuilder.CreateIndex(
                name: "ux_restaurant_working_hours_restaurant_day",
                schema: "core",
                table: "restaurant_working_hours",
                columns: new[] { "restaurant_id", "day_of_week" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_working_hours",
                schema: "core");

            migrationBuilder.DropColumn(
                name: "time_zone",
                schema: "core",
                table: "restaurants");
        }
    }
}
