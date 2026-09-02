using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveRestaurantRoleToAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "role_id",
                schema: "auth",
                table: "user_restaurants",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE auth.user_restaurants ur
                SET role_id = u.role_id
                FROM auth.users u
                WHERE u.id = ur.user_id;
                """);

            migrationBuilder.Sql("""
                UPDATE auth.user_restaurants
                SET role_id = 2
                WHERE role_id IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "role_id",
                schema: "auth",
                table: "user_restaurants",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "user_restaurants_role_id_idx",
                schema: "auth",
                table: "user_restaurants",
                column: "role_id");

            migrationBuilder.AddForeignKey(
                name: "user_restaurants_role_id_fkey",
                schema: "auth",
                table: "user_restaurants",
                column: "role_id",
                principalSchema: "auth",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "user_restaurants_role_id_fkey",
                schema: "auth",
                table: "user_restaurants");

            migrationBuilder.DropIndex(
                name: "user_restaurants_role_id_idx",
                schema: "auth",
                table: "user_restaurants");

            migrationBuilder.DropColumn(
                name: "role_id",
                schema: "auth",
                table: "user_restaurants");
        }
    }
}
