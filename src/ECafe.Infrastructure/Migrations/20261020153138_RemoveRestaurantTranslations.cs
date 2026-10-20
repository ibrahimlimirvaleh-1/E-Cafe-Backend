using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRestaurantTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_group_translations",
                schema: "core");

            migrationBuilder.DropTable(
                name: "restaurant_translations",
                schema: "core");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restaurant_group_translations",
                schema: "core",
                columns: table => new
                {
                    RestaurantGroupId = table.Column<int>(type: "integer", nullable: false),
                    LanguageId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_group_translations", x => new { x.RestaurantGroupId, x.LanguageId });
                    table.ForeignKey(
                        name: "FK_restaurant_group_translations_languages_LanguageId",
                        column: x => x.LanguageId,
                        principalSchema: "core",
                        principalTable: "languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_restaurant_group_translations_restaurant_groups_RestaurantG~",
                        column: x => x.RestaurantGroupId,
                        principalSchema: "core",
                        principalTable: "restaurant_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_translations",
                schema: "core",
                columns: table => new
                {
                    RestaurantId = table.Column<int>(type: "integer", nullable: false),
                    LanguageId = table.Column<int>(type: "integer", nullable: false),
                    BranchName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_translations", x => new { x.RestaurantId, x.LanguageId });
                    table.ForeignKey(
                        name: "FK_restaurant_translations_languages_LanguageId",
                        column: x => x.LanguageId,
                        principalSchema: "core",
                        principalTable: "languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_restaurant_translations_restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalSchema: "core",
                        principalTable: "restaurants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_group_translations_LanguageId",
                schema: "core",
                table: "restaurant_group_translations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_translations_LanguageId",
                schema: "core",
                table: "restaurant_translations",
                column: "LanguageId");
        }
    }
}
