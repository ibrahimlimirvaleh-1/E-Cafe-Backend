using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKitchenCancellationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_cancellation_requests",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ForwardedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ForwardedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_cancellation_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_cancellation_requests_orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "ops",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_cancellation_requests_users_ForwardedByUserId",
                        column: x => x.ForwardedByUserId,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_cancellation_requests_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_cancellation_requests_users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_cancellation_requests_ForwardedByUserId",
                schema: "ops",
                table: "order_cancellation_requests",
                column: "ForwardedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_order_cancellation_requests_OrderId",
                schema: "ops",
                table: "order_cancellation_requests",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_cancellation_requests_RequestedByUserId",
                schema: "ops",
                table: "order_cancellation_requests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_order_cancellation_requests_ResolvedByUserId",
                schema: "ops",
                table: "order_cancellation_requests",
                column: "ResolvedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_cancellation_requests",
                schema: "ops");
        }
    }
}
