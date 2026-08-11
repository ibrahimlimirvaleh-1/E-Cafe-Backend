using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTemporaryFileType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "common",
                table: "file_types",
                columns: new[] { "id", "allowed_extensions", "allowed_mime_types", "code", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsDeleted", "is_public", "max_size_mb", "name", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 8, ".jpg,.jpeg,.png,.webp,.pdf,.doc,.docx", "image/jpeg,image/png,image/webp,application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document", "temporary_upload", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "", null, null, false, false, 10, "Müvəqqəti yükləmə", null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "common",
                table: "file_types",
                keyColumn: "id",
                keyValue: 8);
        }
    }
}
