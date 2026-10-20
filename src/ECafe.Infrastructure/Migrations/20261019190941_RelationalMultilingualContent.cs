using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ECafe.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RelationalMultilingualContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "keywords",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_keywords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "languages",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_languages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "category_translations",
                schema: "catalog",
                columns: table => new
                {
                    LanguageId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_category_translations", x => new { x.CategoryId, x.LanguageId });
                    table.ForeignKey(
                        name: "FK_category_translations_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_category_translations_languages_LanguageId",
                        column: x => x.LanguageId,
                        principalSchema: "core",
                        principalTable: "languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_item_translations",
                schema: "inventory",
                columns: table => new
                {
                    LanguageId = table.Column<int>(type: "integer", nullable: false),
                    InventoryItemId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_item_translations", x => new { x.InventoryItemId, x.LanguageId });
                    table.ForeignKey(
                        name: "FK_inventory_item_translations_inventory_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalSchema: "inventory",
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_item_translations_languages_LanguageId",
                        column: x => x.LanguageId,
                        principalSchema: "core",
                        principalTable: "languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_translations",
                schema: "catalog",
                columns: table => new
                {
                    LanguageId = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UnavailableReason = table.Column<string>(type: "text", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_translations", x => new { x.ItemId, x.LanguageId });
                    table.ForeignKey(
                        name: "FK_item_translations_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "catalog",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_item_translations_languages_LanguageId",
                        column: x => x.LanguageId,
                        principalSchema: "core",
                        principalTable: "languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "keyword_translations",
                schema: "core",
                columns: table => new
                {
                    KeywordId = table.Column<int>(type: "integer", nullable: false),
                    LanguageId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_keyword_translations", x => new { x.KeywordId, x.LanguageId });
                    table.ForeignKey(
                        name: "FK_keyword_translations_keywords_KeywordId",
                        column: x => x.KeywordId,
                        principalSchema: "core",
                        principalTable: "keywords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_keyword_translations_languages_LanguageId",
                        column: x => x.LanguageId,
                        principalSchema: "core",
                        principalTable: "languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_group_translations",
                schema: "core",
                columns: table => new
                {
                    LanguageId = table.Column<int>(type: "integer", nullable: false),
                    RestaurantGroupId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_group_translations", x => new { x.RestaurantGroupId, x.LanguageId });
                    table.ForeignKey(
                        name: "FK_restaurant_group_translations_languages_LanguageId",
                        column: x => x.LanguageId,
                        principalSchema: "core",
                        principalTable: "languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_restaurant_group_translations_restaurant_groups_RestaurantG~",
                        column: x => x.RestaurantGroupId,
                        principalSchema: "core",
                        principalTable: "restaurant_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_translations",
                schema: "core",
                columns: table => new
                {
                    LanguageId = table.Column<int>(type: "integer", nullable: false),
                    RestaurantId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Location = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BranchName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_translations", x => new { x.RestaurantId, x.LanguageId });
                    table.ForeignKey(
                        name: "FK_restaurant_translations_languages_LanguageId",
                        column: x => x.LanguageId,
                        principalSchema: "core",
                        principalTable: "languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_restaurant_translations_restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalSchema: "core",
                        principalTable: "restaurants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "table_translations",
                schema: "ops",
                columns: table => new
                {
                    LanguageId = table.Column<int>(type: "integer", nullable: false),
                    TableId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_table_translations", x => new { x.TableId, x.LanguageId });
                    table.ForeignKey(
                        name: "FK_table_translations_languages_LanguageId",
                        column: x => x.LanguageId,
                        principalSchema: "core",
                        principalTable: "languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_table_translations_tables_TableId",
                        column: x => x.TableId,
                        principalSchema: "ops",
                        principalTable: "tables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "translation_release_entries",
                schema: "core",
                columns: table => new
                {
                    TranslationReleaseId = table.Column<int>(type: "integer", nullable: false),
                    KeywordId = table.Column<int>(type: "integer", nullable: false),
                    LanguageId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_release_entries", x => new { x.TranslationReleaseId, x.KeywordId, x.LanguageId });
                    table.ForeignKey(
                        name: "FK_translation_release_entries_keywords_KeywordId",
                        column: x => x.KeywordId,
                        principalSchema: "core",
                        principalTable: "keywords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_translation_release_entries_languages_LanguageId",
                        column: x => x.LanguageId,
                        principalSchema: "core",
                        principalTable: "languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_translation_release_entries_translation_releases_Translatio~",
                        column: x => x.TranslationReleaseId,
                        principalSchema: "core",
                        principalTable: "translation_releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "unit_translations",
                schema: "inventory",
                columns: table => new
                {
                    LanguageId = table.Column<int>(type: "integer", nullable: false),
                    UnitId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unit_translations", x => new { x.UnitId, x.LanguageId });
                    table.ForeignKey(
                        name: "FK_unit_translations_languages_LanguageId",
                        column: x => x.LanguageId,
                        principalSchema: "core",
                        principalTable: "languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_unit_translations_units_UnitId",
                        column: x => x.UnitId,
                        principalSchema: "inventory",
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "core",
                table: "languages",
                columns: new[] { "Id", "Code", "IsActive", "IsDefault", "Name" },
                values: new object[,]
                {
                    { 1, "az", true, true, "Azərbaycan" },
                    { 2, "en", true, false, "English" },
                    { 3, "ru", true, false, "Русский" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_category_translations_LanguageId",
                schema: "catalog",
                table: "category_translations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_item_translations_LanguageId",
                schema: "inventory",
                table: "inventory_item_translations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_item_translations_LanguageId",
                schema: "catalog",
                table: "item_translations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_keyword_translations_LanguageId",
                schema: "core",
                table: "keyword_translations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_keywords_Key",
                schema: "core",
                table: "keywords",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_languages_Code",
                schema: "core",
                table: "languages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_group_translations_LanguageId",
                schema: "core",
                table: "restaurant_group_translations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_translations_LanguageId",
                schema: "core",
                table: "restaurant_translations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_table_translations_LanguageId",
                schema: "ops",
                table: "table_translations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_translation_release_entries_KeywordId",
                schema: "core",
                table: "translation_release_entries",
                column: "KeywordId");

            migrationBuilder.CreateIndex(
                name: "IX_translation_release_entries_LanguageId",
                schema: "core",
                table: "translation_release_entries",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_unit_translations_LanguageId",
                schema: "inventory",
                table: "unit_translations",
                column: "LanguageId");

            SeedAndMigrateTranslations(migrationBuilder);
            migrationBuilder.DropColumn(name: "MessagesJson", schema: "core", table: "translation_releases");
            migrationBuilder.DropColumn(name: "DraftJson", schema: "core", table: "translation_catalog");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM core.restaurant_translations WHERE "LanguageId" <> 1)
                    OR EXISTS (SELECT 1 FROM core.restaurant_group_translations WHERE "LanguageId" <> 1)
                    OR EXISTS (SELECT 1 FROM catalog.category_translations WHERE "LanguageId" <> 1)
                    OR EXISTS (SELECT 1 FROM catalog.item_translations WHERE "LanguageId" <> 1)
                    OR EXISTS (SELECT 1 FROM ops.table_translations WHERE "LanguageId" <> 1)
                    OR EXISTS (SELECT 1 FROM inventory.inventory_item_translations WHERE "LanguageId" <> 1)
                    OR EXISTS (SELECT 1 FROM inventory.unit_translations WHERE "LanguageId" <> 1)
                  THEN RAISE EXCEPTION 'Export multilingual business content before downgrading; the old schema cannot retain it.';
                  END IF;
                END $$;
                """);
            migrationBuilder.AddColumn<string>(name: "MessagesJson", schema: "core", table: "translation_releases",
                type: "jsonb", nullable: false, defaultValue: "{}");
            migrationBuilder.AddColumn<string>(name: "DraftJson", schema: "core", table: "translation_catalog",
                type: "jsonb", nullable: false, defaultValue: "{}");
            migrationBuilder.Sql("""
                UPDATE core.translation_catalog SET "DraftJson" = COALESCE((
                  SELECT jsonb_object_agg(grouped.key, grouped.texts) FROM (
                    SELECT k."Key" AS key, jsonb_object_agg(l."Code", t."Value") AS texts
                    FROM core.keyword_translations t JOIN core.keywords k ON k."Id" = t."KeywordId"
                    JOIN core.languages l ON l."Id" = t."LanguageId" GROUP BY k."Key"
                  ) grouped), '{}'::jsonb) WHERE "Id" = 1;
                UPDATE core.translation_releases r SET "MessagesJson" = COALESCE((
                  SELECT jsonb_object_agg(grouped.key, grouped.texts) FROM (
                    SELECT k."Key" AS key, jsonb_object_agg(l."Code", t."Value") AS texts
                    FROM core.translation_release_entries t JOIN core.keywords k ON k."Id" = t."KeywordId"
                    JOIN core.languages l ON l."Id" = t."LanguageId"
                    WHERE t."TranslationReleaseId" = r."Id" GROUP BY k."Key"
                  ) grouped), '{}'::jsonb);
                """);
            migrationBuilder.DropTable(
                name: "category_translations",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "inventory_item_translations",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "item_translations",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "keyword_translations",
                schema: "core");

            migrationBuilder.DropTable(
                name: "restaurant_group_translations",
                schema: "core");

            migrationBuilder.DropTable(
                name: "restaurant_translations",
                schema: "core");

            migrationBuilder.DropTable(
                name: "table_translations",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "translation_release_entries",
                schema: "core");

            migrationBuilder.DropTable(
                name: "unit_translations",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "keywords",
                schema: "core");

            migrationBuilder.DropTable(
                name: "languages",
                schema: "core");

        }

        /// <summary>Dondurulmuş ilkin lüğəti və əvvəlki JSON redaktə/yayım tarixçəsini köçürür; köhnə sütunlar yalnız bundan sonra silinir.</summary>
        private static void SeedAndMigrateTranslations(MigrationBuilder migrationBuilder)
        {
            // İlkin lüğət ayrıca dondurulmuş migration mənbəyindən əlavə olunur.
            migrationBuilder.Sql(Seeds.RelationalTranslationCatalogSeed.ReadSql());
            migrationBuilder.Sql("""
                INSERT INTO core.keywords ("Key")
                SELECT key FROM (
                  SELECT jsonb_object_keys("DraftJson") AS key FROM core.translation_catalog
                  UNION SELECT jsonb_object_keys("MessagesJson") FROM core.translation_releases
                ) old_keys ON CONFLICT ("Key") DO NOTHING;

                INSERT INTO core.keyword_translations ("KeywordId", "LanguageId", "Value")
                SELECT k."Id", l."Id", e.value ->> l."Code" FROM core.translation_catalog c
                CROSS JOIN LATERAL jsonb_each(c."DraftJson") e
                JOIN core.keywords k ON k."Key" = e.key CROSS JOIN core.languages l
                WHERE l."Code" IN ('az', 'en', 'ru') AND e.value ->> l."Code" IS NOT NULL
                ON CONFLICT ("KeywordId", "LanguageId") DO UPDATE SET "Value" = EXCLUDED."Value";

                INSERT INTO core.translation_release_entries ("TranslationReleaseId", "KeywordId", "LanguageId", "Value")
                SELECT r."Id", k."Id", l."Id", e.value ->> l."Code" FROM core.translation_releases r
                CROSS JOIN LATERAL jsonb_each(r."MessagesJson") e
                JOIN core.keywords k ON k."Key" = e.key CROSS JOIN core.languages l
                WHERE l."Code" IN ('az', 'en', 'ru') AND e.value ->> l."Code" IS NOT NULL;

                INSERT INTO core.restaurant_translations ("RestaurantId", "LanguageId", "Revision", "Name", "Location", "BranchName")
                  SELECT id, 1, 1, name, location, branch_name FROM core.restaurants;
                INSERT INTO core.restaurant_group_translations ("RestaurantGroupId", "LanguageId", "Revision", "Name")
                  SELECT id, 1, 1, name FROM core.restaurant_groups;
                INSERT INTO catalog.category_translations ("CategoryId", "LanguageId", "Revision", "Name")
                  SELECT id, 1, 1, name FROM catalog.categories;
                INSERT INTO catalog.item_translations ("ItemId", "LanguageId", "Revision", "Name", "Description", "UnavailableReason")
                  SELECT id, 1, 1, name, description, unavailable_reason FROM catalog.items;
                INSERT INTO ops.table_translations ("TableId", "LanguageId", "Revision", "Name")
                  SELECT id, 1, 1, name FROM ops.tables;
                INSERT INTO inventory.inventory_item_translations ("InventoryItemId", "LanguageId", "Revision", "Name")
                  SELECT id, 1, 1, name FROM inventory.inventory_items;
                INSERT INTO inventory.unit_translations ("UnitId", "LanguageId", "Revision", "Name")
                  SELECT id, 1, 1, name FROM inventory.units;
                """);
        }
    }
}
