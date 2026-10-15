using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCreationIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "creation_request_hash",
                schema: "ops",
                table: "orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "creation_request_id",
                schema: "ops",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "orders_session_creation_request_uidx",
                schema: "ops",
                table: "orders",
                columns: new[] { "table_session_id", "creation_request_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "orders_session_creation_request_uidx",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "creation_request_hash",
                schema: "ops",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "creation_request_id",
                schema: "ops",
                table: "orders");
        }
    }
}
