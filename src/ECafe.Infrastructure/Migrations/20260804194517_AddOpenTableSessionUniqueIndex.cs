using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenTableSessionUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_table_sessions_open_table",
                schema: "ops",
                table: "table_sessions",
                columns: new[] { "restaurant_id", "table_id" },
                unique: true,
                filter: "status_id = 7001 AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_table_sessions_open_table",
                schema: "ops",
                table: "table_sessions");
        }
    }
}
