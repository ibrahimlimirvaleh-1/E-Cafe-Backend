using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RoleTargetedProductAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.CreateTable(
                name: "product_announcements",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'2', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Destination = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_announcements", x => x.Id);
                    table.CheckConstraint("announcement_version_positive", "\"Version\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "product_announcement_receipts",
                schema: "platform",
                columns: table => new
                {
                    AnnouncementId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_announcement_receipts", x => new { x.UserId, x.RoleId, x.AnnouncementId, x.Version });
                    table.ForeignKey(
                        name: "FK_product_announcement_receipts_product_announcements_Announc~",
                        column: x => x.AnnouncementId,
                        principalSchema: "platform",
                        principalTable: "product_announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_announcement_receipts_roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "auth",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_announcement_receipts_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "auth",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_announcement_roles",
                schema: "platform",
                columns: table => new
                {
                    AnnouncementId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_announcement_roles", x => new { x.AnnouncementId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_product_announcement_roles_product_announcements_Announceme~",
                        column: x => x.AnnouncementId,
                        principalSchema: "platform",
                        principalTable: "product_announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_announcement_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "auth",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_announcement_texts",
                schema: "platform",
                columns: table => new
                {
                    AnnouncementId = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_announcement_texts", x => new { x.AnnouncementId, x.Language });
                    table.ForeignKey(
                        name: "FK_product_announcement_texts_product_announcements_Announceme~",
                        column: x => x.AnnouncementId,
                        principalSchema: "platform",
                        principalTable: "product_announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "product_announcements",
                columns: new[] { "Id", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Destination", "ExpiresAtUtc", "IsDeleted", "PublishedAtUtc", "UpdatedAt", "UpdatedBy", "Version" },
                values: new object[] { 1, new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "CustomerProfile", null, false, null, null, null, 1 });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "product_announcement_roles",
                columns: new[] { "AnnouncementId", "RoleId" },
                values: new object[] { 1, 5 });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "product_announcement_texts",
                columns: new[] { "AnnouncementId", "Language", "Body", "Title" },
                values: new object[,]
                {
                    { 1, "az", "Profilinizin Təhlükəsizlik bölməsindən hesabınızı bağlaya bilərsiniz. Aktiv rezervasiya, açıq masa və ya ödənilməmiş hesab varsa, əvvəl onları tamamlamalısınız. Profil məlumatları anonimləşdirilir, sifariş və maliyyə tarixçəsi saxlanılır.", "Hesabınızı özünüz silə bilərsiniz" },
                    { 1, "en", "You can close your account in Profile > Security. Complete active reservations, open table sessions and outstanding payments first. Profile details are anonymized; order and financial history is retained.", "You can now delete your own account" },
                    { 1, "ru", "Закрыть аккаунт можно в разделе Профиль > Безопасность. Сначала завершите активные бронирования, сеансы за столом и неоплаченные счета. Данные профиля обезличиваются, история заказов и финансовых операций сохраняется.", "Теперь вы можете удалить свой аккаунт" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_announcement_receipts_AnnouncementId",
                schema: "platform",
                table: "product_announcement_receipts",
                column: "AnnouncementId");

            migrationBuilder.CreateIndex(
                name: "IX_product_announcement_receipts_RoleId",
                schema: "platform",
                table: "product_announcement_receipts",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_product_announcement_roles_RoleId_AnnouncementId",
                schema: "platform",
                table: "product_announcement_roles",
                columns: new[] { "RoleId", "AnnouncementId" });

            migrationBuilder.CreateIndex(
                name: "IX_product_announcements_PublishedAtUtc_Id",
                schema: "platform",
                table: "product_announcements",
                columns: new[] { "PublishedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_announcement_receipts",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "product_announcement_roles",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "product_announcement_texts",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "product_announcements",
                schema: "platform");
        }
    }
}
