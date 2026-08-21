using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ECafeDbContext))]
    [Migration("20260821103000_AddRoleStaffAssignableFlag")]
    public partial class AddRoleStaffAssignableFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_staff_assignable",
                schema: "auth",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE auth.roles
                SET is_staff_assignable = TRUE
                WHERE id IN (2, 3, 4, 6);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_staff_assignable",
                schema: "auth",
                table: "roles");
        }
    }
}
