using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveRestaurantEmailToGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "restaurants_email_key",
                schema: "core",
                table: "restaurants");

            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "core",
                table: "restaurant_groups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE core.restaurant_groups rg
                SET email = source.email
                FROM (
                    SELECT DISTINCT ON (restaurant_group_id)
                        restaurant_group_id,
                        email
                    FROM core.restaurants
                    WHERE restaurant_group_id IS NOT NULL
                      AND email IS NOT NULL
                      AND btrim(email) <> ''
                    ORDER BY restaurant_group_id, id
                ) AS source
                WHERE rg.id = source.restaurant_group_id
                  AND rg.email IS NULL;
                """);

            migrationBuilder.DropColumn(
                name: "email",
                schema: "core",
                table: "restaurants");

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_groups_email",
                schema: "core",
                table: "restaurant_groups",
                column: "email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_restaurant_groups_email",
                schema: "core",
                table: "restaurant_groups");

            migrationBuilder.DropColumn(
                name: "email",
                schema: "core",
                table: "restaurant_groups");

            migrationBuilder.AddColumn<string>(
                name: "email",
                schema: "core",
                table: "restaurants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "restaurants_email_key",
                schema: "core",
                table: "restaurants",
                column: "email",
                unique: true);
        }
    }
}
