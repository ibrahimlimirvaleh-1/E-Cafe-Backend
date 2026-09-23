using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations;

[Migration("20260923090000_FixReservationWorkflowActionEndpoints")]
public partial class FixReservationWorkflowActionEndpoints : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE core.workflow_action_rules
            SET endpoint_template = '/api/v1/public/reservations/{reservationId}/cancel'
            WHERE flow_code = 'reservation'
              AND action_code = 'cancel'
              AND role_id = 5;

            UPDATE core.workflow_action_rules
            SET endpoint_template = '/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel'
            WHERE flow_code = 'reservation'
              AND action_code = 'cancel'
              AND role_id = 1;

            UPDATE core.workflow_action_rules
            SET is_enabled = false
            WHERE flow_code = 'reservation'
              AND action_code IN ('checkIn', 'markNoShow', 'complete');

            UPDATE core.workflow_action_rules
            SET is_enabled = false
            WHERE flow_code IN ('order', 'kitchen', 'payment');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE core.workflow_action_rules
            SET endpoint_template = '/api/v1/restaurants/{restaurantId}/reservations/{reservationId}/cancel'
            WHERE flow_code = 'reservation'
              AND action_code = 'cancel'
              AND role_id = 5;

            UPDATE core.workflow_action_rules
            SET endpoint_template = '/api/v1/admin/restaurants/{restaurantId}/reservations/{reservationId}/cancel'
            WHERE flow_code = 'reservation'
              AND action_code = 'cancel'
              AND role_id = 1;

            UPDATE core.workflow_action_rules
            SET is_enabled = true
            WHERE flow_code = 'reservation'
              AND action_code IN ('checkIn', 'markNoShow', 'complete');

            UPDATE core.workflow_action_rules
            SET is_enabled = CASE
                WHEN flow_code = 'payment' AND action_code = 'pay' THEN false
                ELSE true
            END
            WHERE flow_code IN ('order', 'kitchen', 'payment');
            """);
    }
}
