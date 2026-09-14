using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkingHourCloseDayOffset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_restaurant_working_hours_open_closed_not_equal",
                schema: "core",
                table: "restaurant_working_hours");

            migrationBuilder.AddColumn<int>(
                name: "close_day_offset",
                schema: "core",
                table: "restaurant_working_hours",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE core.restaurant_working_hours
                SET close_day_offset = 1
                WHERE is_closed = false AND opens_at > closes_at;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_restaurant_working_hours_close_day_offset",
                schema: "core",
                table: "restaurant_working_hours",
                sql: "close_day_offset >= 0 AND close_day_offset <= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_restaurant_working_hours_open_closed_not_equal",
                schema: "core",
                table: "restaurant_working_hours",
                sql: "is_closed = true OR close_day_offset = 1 OR opens_at <> closes_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_restaurant_working_hours_close_day_offset",
                schema: "core",
                table: "restaurant_working_hours");

            migrationBuilder.DropCheckConstraint(
                name: "ck_restaurant_working_hours_open_closed_not_equal",
                schema: "core",
                table: "restaurant_working_hours");

            migrationBuilder.DropColumn(
                name: "close_day_offset",
                schema: "core",
                table: "restaurant_working_hours");

            migrationBuilder.AddCheckConstraint(
                name: "ck_restaurant_working_hours_open_closed_not_equal",
                schema: "core",
                table: "restaurant_working_hours",
                sql: "is_closed = true OR opens_at <> closes_at");
        }
    }
}
