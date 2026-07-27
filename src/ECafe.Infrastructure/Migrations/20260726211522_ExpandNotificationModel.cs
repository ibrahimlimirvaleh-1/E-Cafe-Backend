using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandNotificationModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_UserId",
                schema: "notification",
                table: "notifications");

            migrationBuilder.AddColumn<int>(
                name: "ChannelId",
                schema: "notification",
                table: "notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PayloadJson",
                schema: "notification",
                table: "notifications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RelatedEntityId",
                schema: "notification",
                table: "notifications",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedEntityType",
                schema: "notification",
                table: "notifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantId",
                schema: "notification",
                table: "notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusId",
                schema: "notification",
                table: "notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TypeId",
                schema: "notification",
                table: "notifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_RelatedEntityType_RelatedEntityId",
                schema: "notification",
                table: "notifications",
                columns: new[] { "RelatedEntityType", "RelatedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_RestaurantId_CreatedAt",
                schema: "notification",
                table: "notifications",
                columns: new[] { "RestaurantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_IsRead_CreatedAt",
                schema: "notification",
                table: "notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_RelatedEntityType_RelatedEntityId",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_RestaurantId_CreatedAt",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_UserId_IsRead_CreatedAt",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "ChannelId",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "PayloadJson",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "RelatedEntityId",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "RelatedEntityType",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "RestaurantId",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "StatusId",
                schema: "notification",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "TypeId",
                schema: "notification",
                table: "notifications");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId",
                schema: "notification",
                table: "notifications",
                column: "UserId");
        }
    }
}
