using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWaiterTableLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "max_active_table_count",
                schema: "auth",
                table: "user_restaurants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "default_waiter_table_limit",
                schema: "core",
                table: "restaurants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_restaurants_max_active_table_count_positive",
                schema: "auth",
                table: "user_restaurants",
                sql: "max_active_table_count IS NULL OR max_active_table_count > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_restaurants_default_waiter_table_limit_positive",
                schema: "core",
                table: "restaurants",
                sql: "default_waiter_table_limit IS NULL OR default_waiter_table_limit > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_restaurants_max_active_table_count_positive",
                schema: "auth",
                table: "user_restaurants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_restaurants_default_waiter_table_limit_positive",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "max_active_table_count",
                schema: "auth",
                table: "user_restaurants");

            migrationBuilder.DropColumn(
                name: "default_waiter_table_limit",
                schema: "core",
                table: "restaurants");
        }
    }
}
