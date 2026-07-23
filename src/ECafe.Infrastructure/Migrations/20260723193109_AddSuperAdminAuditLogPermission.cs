using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperAdminAuditLogPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO auth.role_permisions
                    ("Id", permission_id, role_id, "CreatedAt", "CreatedBy", "IsDeleted")
                SELECT
                    COALESCE(MAX("Id"), 0) + 1, 17, 1, TIMESTAMP '-infinity', '', false
                FROM auth.role_permisions
                ON CONFLICT (role_id, permission_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "auth",
                table: "role_permisions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { 17, 1 });
        }
    }
}
