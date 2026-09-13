using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeriveTableEmptyStateFromSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_empty",
                schema: "ops",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "reserved_from",
                schema: "ops",
                table: "reservations");

            migrationBuilder.RenameColumn(
                name: "reserved_to",
                schema: "ops",
                table: "reservations",
                newName: "reserved_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "reserved_at",
                schema: "ops",
                table: "reservations",
                newName: "reserved_to");

            migrationBuilder.AddColumn<bool>(
                name: "is_empty",
                schema: "ops",
                table: "tables",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reserved_from",
                schema: "ops",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
