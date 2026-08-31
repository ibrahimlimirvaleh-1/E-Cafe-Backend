using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantLocationCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_restaurants_default_waiter_table_limit_positive",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "default_waiter_table_limit",
                schema: "core",
                table: "restaurants");

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                schema: "core",
                table: "restaurants",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                schema: "core",
                table: "restaurants",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "place_id",
                schema: "core",
                table: "restaurants",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_restaurants_latitude_range",
                schema: "core",
                table: "restaurants",
                sql: "latitude IS NULL OR (latitude >= -90 AND latitude <= 90)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_restaurants_longitude_range",
                schema: "core",
                table: "restaurants",
                sql: "longitude IS NULL OR (longitude >= -180 AND longitude <= 180)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_restaurants_latitude_range",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_restaurants_longitude_range",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "latitude",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "longitude",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "place_id",
                schema: "core",
                table: "restaurants");

            migrationBuilder.AddColumn<int>(
                name: "default_waiter_table_limit",
                schema: "core",
                table: "restaurants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_restaurants_default_waiter_table_limit_positive",
                schema: "core",
                table: "restaurants",
                sql: "default_waiter_table_limit IS NULL OR default_waiter_table_limit > 0");
        }
    }
}
