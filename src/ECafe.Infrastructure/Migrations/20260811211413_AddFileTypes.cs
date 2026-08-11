using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "file_types",
                schema: "common",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    allowed_extensions = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    allowed_mime_types = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    max_size_mb = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("file_types_pkey", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "common",
                table: "file_types",
                columns: new[] { "id", "allowed_extensions", "allowed_mime_types", "code", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "is_public", "max_size_mb", "name", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, ".jpg,.jpeg,.png,.webp", "image/jpeg,image/png,image/webp", "restaurant_image", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, true, 10, "Restoran şəkli", null, null },
                    { 2, ".jpg,.jpeg,.png,.webp", "image/jpeg,image/png,image/webp", "menu_item_image", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, true, 10, "Menyu elementi şəkli", null, null },
                    { 3, ".jpg,.jpeg,.png,.webp", "image/jpeg,image/png,image/webp", "user_profile_image", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, true, 5, "Profil şəkli", null, null },
                    { 4, ".pdf,.doc,.docx", "application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document", "contract_document", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, false, 10, "Müqavilə sənədi", null, null },
                    { 5, ".pdf", "application/pdf", "invoice_document", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, false, 10, "Invoice sənədi", null, null },
                    { 6, ".pdf,.jpg,.jpeg,.png,.webp", "application/pdf,image/jpeg,image/png,image/webp", "payment_receipt", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, false, 10, "Ödəniş qəbzi", null, null },
                    { 7, ".pdf,.doc,.docx", "application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document", "admin_document", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, false, 10, "Admin sənədi", null, null }
                });

            migrationBuilder.AddColumn<int>(
                name: "file_type_id",
                schema: "common",
                table: "files",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE common.files AS f
                SET file_type_id = 4
                WHERE EXISTS (
                    SELECT 1
                    FROM core.restaurant_contracts AS rc
                    WHERE rc.file_id = f.id
                );

                UPDATE common.files AS f
                SET file_type_id = 2
                WHERE file_type_id IS NULL
                  AND EXISTS (
                    SELECT 1
                    FROM catalog.items AS i
                    WHERE i.file_id = f.id
                  );

                UPDATE common.files AS f
                SET file_type_id = 3
                WHERE file_type_id IS NULL
                  AND EXISTS (
                    SELECT 1
                    FROM auth.users AS u
                    WHERE u.file_id = f.id
                  );

                UPDATE common.files
                SET file_type_id = 1
                WHERE file_type_id IS NULL
                  AND "RestaurantId" IS NOT NULL;

                UPDATE common.files
                SET file_type_id = 7
                WHERE file_type_id IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "file_type_id",
                schema: "common",
                table: "files",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_files_file_type_id",
                schema: "common",
                table: "files",
                column: "file_type_id");

            migrationBuilder.CreateIndex(
                name: "file_types_code_key",
                schema: "common",
                table: "file_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "file_types_name_key",
                schema: "common",
                table: "file_types",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_files_file_types_file_type_id",
                schema: "common",
                table: "files",
                column: "file_type_id",
                principalSchema: "common",
                principalTable: "file_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_files_file_types_file_type_id",
                schema: "common",
                table: "files");

            migrationBuilder.DropTable(
                name: "file_types",
                schema: "common");

            migrationBuilder.DropIndex(
                name: "IX_files_file_type_id",
                schema: "common",
                table: "files");

            migrationBuilder.DropColumn(
                name: "file_type_id",
                schema: "common",
                table: "files");
        }
    }
}
