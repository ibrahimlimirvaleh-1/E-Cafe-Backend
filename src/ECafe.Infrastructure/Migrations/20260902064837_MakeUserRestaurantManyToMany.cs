using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeUserRestaurantManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "user_restaurants_user_id_key",
                schema: "auth",
                table: "user_restaurants");

            migrationBuilder.UpdateData(
                schema: "auth",
                table: "roles",
                keyColumn: "id",
                keyValue: 2,
                column: "is_staff_assignable",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "auth",
                table: "roles",
                keyColumn: "id",
                keyValue: 2,
                column: "is_staff_assignable",
                value: true);

            migrationBuilder.CreateIndex(
                name: "user_restaurants_user_id_key",
                schema: "auth",
                table: "user_restaurants",
                column: "user_id",
                unique: true);
        }
    }
}
