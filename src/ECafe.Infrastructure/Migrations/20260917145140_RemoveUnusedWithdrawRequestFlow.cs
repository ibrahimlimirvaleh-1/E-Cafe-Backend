using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedWithdrawRequestFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_wallet_transactions_withdraw_requests_WithdrawRequestId",
                schema: "billing",
                table: "wallet_transactions");

            migrationBuilder.DropTable(
                name: "withdraw_requests",
                schema: "billing");

            migrationBuilder.DropIndex(
                name: "IX_wallet_transactions_WithdrawRequestId",
                schema: "billing",
                table: "wallet_transactions");

            migrationBuilder.DropColumn(
                name: "WithdrawRequestId",
                schema: "billing",
                table: "wallet_transactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WithdrawRequestId",
                schema: "billing",
                table: "wallet_transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "withdraw_requests",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    WalletId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withdraw_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_withdraw_requests_statuses_StatusId",
                        column: x => x.StatusId,
                        principalSchema: "auth",
                        principalTable: "statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_withdraw_requests_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_withdraw_requests_wallets_WalletId",
                        column: x => x.WalletId,
                        principalSchema: "billing",
                        principalTable: "wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_WithdrawRequestId",
                schema: "billing",
                table: "wallet_transactions",
                column: "WithdrawRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_withdraw_requests_ApprovedByUserId",
                schema: "billing",
                table: "withdraw_requests",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_withdraw_requests_StatusId",
                schema: "billing",
                table: "withdraw_requests",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_withdraw_requests_WalletId",
                schema: "billing",
                table: "withdraw_requests",
                column: "WalletId");

            migrationBuilder.AddForeignKey(
                name: "FK_wallet_transactions_withdraw_requests_WithdrawRequestId",
                schema: "billing",
                table: "wallet_transactions",
                column: "WithdrawRequestId",
                principalSchema: "billing",
                principalTable: "withdraw_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
