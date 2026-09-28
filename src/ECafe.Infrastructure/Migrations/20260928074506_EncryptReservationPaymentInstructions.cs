using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EncryptReservationPaymentInstructions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "display_text",
                schema: "billing",
                table: "reservation_payment_instructions",
                newName: "legacy_display_text");

            migrationBuilder.AlterColumn<string>(
                name: "legacy_display_text",
                schema: "billing",
                table: "reservation_payment_instructions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<string>(
                name: "encrypted_details",
                schema: "billing",
                table: "reservation_payment_instructions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "masked_details",
                schema: "billing",
                table: "reservation_payment_instructions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new global::System.InvalidOperationException(
                "This migration cannot be rolled back after payment instructions are encrypted.");
        }
    }
}
