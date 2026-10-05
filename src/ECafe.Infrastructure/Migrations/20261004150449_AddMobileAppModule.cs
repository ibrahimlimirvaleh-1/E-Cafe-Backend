using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileAppModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "mobile_push_enabled",
                schema: "core",
                table: "restaurants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "show_mobile_download_link",
                schema: "core",
                table: "restaurants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "mobile_app_publication",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    public_download_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mobile_app_publication", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "core",
                table: "mobile_app_publication",
                columns: new[] { "id", "public_download_enabled" },
                values: new object[] { 1, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mobile_app_publication",
                schema: "core");

            migrationBuilder.DropColumn(
                name: "mobile_push_enabled",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "show_mobile_download_link",
                schema: "core",
                table: "restaurants");
        }
    }
}
