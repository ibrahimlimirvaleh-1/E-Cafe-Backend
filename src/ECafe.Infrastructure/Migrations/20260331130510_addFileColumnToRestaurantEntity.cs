using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addFileColumnToRestaurantEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "rating_count",
                schema: "core",
                table: "restaurants",
                type: "integer",
                nullable: true,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "rating_average",
                schema: "core",
                table: "restaurants",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(3,2)",
                oldPrecision: 3,
                oldScale: 2);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                schema: "common",
                table: "files",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_files_RestaurantId",
                schema: "common",
                table: "files",
                column: "RestaurantId");

            migrationBuilder.AddForeignKey(
                name: "FK_files_restaurants_RestaurantId",
                schema: "common",
                table: "files",
                column: "RestaurantId",
                principalSchema: "core",
                principalTable: "restaurants",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_files_restaurants_RestaurantId",
                schema: "common",
                table: "files");

            migrationBuilder.DropIndex(
                name: "IX_files_RestaurantId",
                schema: "common",
                table: "files");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                schema: "common",
                table: "files");

            migrationBuilder.AlterColumn<int>(
                name: "rating_count",
                schema: "core",
                table: "restaurants",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true,
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "rating_average",
                schema: "core",
                table: "restaurants",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(3,2)",
                oldPrecision: 3,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
