using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakePaymentApprovalConfirmationOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE core.workflow_action_rules
                SET requires_confirmation = FALSE
                WHERE flow_code = 'reservation'
                  AND action_code = 'approvePaymentProof';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE core.workflow_action_rules
                SET requires_confirmation = TRUE
                WHERE flow_code = 'reservation'
                  AND action_code = 'approvePaymentProof';
                """);
        }
    }
}
