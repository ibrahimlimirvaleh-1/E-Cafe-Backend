using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SetRestaurantDefaultTimeZoneToAsiaBaku : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE core.restaurants
                ALTER COLUMN time_zone SET DEFAULT 'Asia/Baku';

                UPDATE core.restaurants
                SET time_zone = 'Asia/Baku'
                WHERE time_zone = 'UTC'
                  AND "IsDeleted" = false;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE core.restaurants
                ALTER COLUMN time_zone SET DEFAULT 'UTC';

                UPDATE core.restaurants
                SET time_zone = 'UTC'
                WHERE time_zone = 'Asia/Baku'
                  AND "IsDeleted" = false;
                """);

        }
    }
}
