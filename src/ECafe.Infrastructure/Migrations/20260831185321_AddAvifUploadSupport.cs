using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAvifUploadSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "common",
                table: "file_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "allowed_extensions", "allowed_mime_types" },
                values: new object[] { ".jpg,.jpeg,.png,.webp,.avif", "image/jpeg,image/png,image/webp,image/avif" });

            migrationBuilder.UpdateData(
                schema: "common",
                table: "file_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "allowed_extensions", "allowed_mime_types" },
                values: new object[] { ".jpg,.jpeg,.png,.webp,.avif", "image/jpeg,image/png,image/webp,image/avif" });

            migrationBuilder.UpdateData(
                schema: "common",
                table: "file_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "allowed_extensions", "allowed_mime_types" },
                values: new object[] { ".jpg,.jpeg,.png,.webp,.avif", "image/jpeg,image/png,image/webp,image/avif" });

            migrationBuilder.UpdateData(
                schema: "common",
                table: "file_types",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "allowed_extensions", "allowed_mime_types" },
                values: new object[] { ".pdf,.jpg,.jpeg,.png,.webp,.avif", "application/pdf,image/jpeg,image/png,image/webp,image/avif" });

            migrationBuilder.UpdateData(
                schema: "common",
                table: "file_types",
                keyColumn: "id",
                keyValue: 8,
                columns: new[] { "allowed_extensions", "allowed_mime_types" },
                values: new object[] { ".jpg,.jpeg,.png,.webp,.avif,.pdf,.doc,.docx", "image/jpeg,image/png,image/webp,image/avif,application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "common",
                table: "file_types",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "allowed_extensions", "allowed_mime_types" },
                values: new object[] { ".jpg,.jpeg,.png,.webp", "image/jpeg,image/png,image/webp" });

            migrationBuilder.UpdateData(
                schema: "common",
                table: "file_types",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "allowed_extensions", "allowed_mime_types" },
                values: new object[] { ".jpg,.jpeg,.png,.webp", "image/jpeg,image/png,image/webp" });

            migrationBuilder.UpdateData(
                schema: "common",
                table: "file_types",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "allowed_extensions", "allowed_mime_types" },
                values: new object[] { ".jpg,.jpeg,.png,.webp", "image/jpeg,image/png,image/webp" });

            migrationBuilder.UpdateData(
                schema: "common",
                table: "file_types",
                keyColumn: "id",
                keyValue: 6,
                columns: new[] { "allowed_extensions", "allowed_mime_types" },
                values: new object[] { ".pdf,.jpg,.jpeg,.png,.webp", "application/pdf,image/jpeg,image/png,image/webp" });

            migrationBuilder.UpdateData(
                schema: "common",
                table: "file_types",
                keyColumn: "id",
                keyValue: 8,
                columns: new[] { "allowed_extensions", "allowed_mime_types" },
                values: new object[] { ".jpg,.jpeg,.png,.webp,.pdf,.doc,.docx", "image/jpeg,image/png,image/webp,application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document" });
        }
    }
}
