using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestoreSingleUserRestaurantAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "user_restaurants_user_id_idx",
                schema: "auth",
                table: "user_restaurants");

            migrationBuilder.CreateIndex(
                name: "user_restaurants_user_id_key",
                schema: "auth",
                table: "user_restaurants",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "user_restaurants_user_id_key",
                schema: "auth",
                table: "user_restaurants");

            migrationBuilder.CreateIndex(
                name: "user_restaurants_user_id_idx",
                schema: "auth",
                table: "user_restaurants",
                column: "user_id");
        }
    }
}
