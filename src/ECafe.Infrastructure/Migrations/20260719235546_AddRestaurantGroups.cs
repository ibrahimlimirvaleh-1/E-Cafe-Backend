using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "branch_name",
                schema: "core",
                table: "restaurants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "restaurant_group_id",
                schema: "core",
                table: "restaurants",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "restaurant_groups",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    owner_user_id = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("restaurant_groups_pkey", x => x.id);
                    table.ForeignKey(
                        name: "restaurant_groups_owner_user_id_fkey",
                        column: x => x.owner_user_id,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_restaurants_restaurant_group_id",
                schema: "core",
                table: "restaurants",
                column: "restaurant_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_groups_owner_user_id",
                schema: "core",
                table: "restaurant_groups",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "restaurant_groups_name_key",
                schema: "core",
                table: "restaurant_groups",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "restaurants_restaurant_group_id_fkey",
                schema: "core",
                table: "restaurants",
                column: "restaurant_group_id",
                principalSchema: "core",
                principalTable: "restaurant_groups",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "restaurants_restaurant_group_id_fkey",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropTable(
                name: "restaurant_groups",
                schema: "core");

            migrationBuilder.DropIndex(
                name: "IX_restaurants_restaurant_group_id",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "branch_name",
                schema: "core",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "restaurant_group_id",
                schema: "core",
                table: "restaurants");
        }
    }
}
