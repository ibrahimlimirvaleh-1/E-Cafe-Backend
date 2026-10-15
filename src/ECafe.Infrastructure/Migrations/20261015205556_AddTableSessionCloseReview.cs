using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTableSessionCloseReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "close_review_confirmed_at",
                schema: "ops",
                table: "table_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "close_review_confirmed_by_user_id",
                schema: "ops",
                table: "table_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "close_review_due_at",
                schema: "ops",
                table: "table_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "close_review_notified_at",
                schema: "ops",
                table: "table_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "close_review_reason",
                schema: "ops",
                table: "table_sessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "table_sessions_close_review_idx",
                schema: "ops",
                table: "table_sessions",
                columns: new[] { "close_review_notified_at", "close_review_due_at" },
                filter: "status_id = 7001 AND closed_at IS NULL AND \"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "table_sessions_close_review_idx",
                schema: "ops",
                table: "table_sessions");

            migrationBuilder.DropColumn(
                name: "close_review_confirmed_at",
                schema: "ops",
                table: "table_sessions");

            migrationBuilder.DropColumn(
                name: "close_review_confirmed_by_user_id",
                schema: "ops",
                table: "table_sessions");

            migrationBuilder.DropColumn(
                name: "close_review_due_at",
                schema: "ops",
                table: "table_sessions");

            migrationBuilder.DropColumn(
                name: "close_review_notified_at",
                schema: "ops",
                table: "table_sessions");

            migrationBuilder.DropColumn(
                name: "close_review_reason",
                schema: "ops",
                table: "table_sessions");
        }
    }
}
