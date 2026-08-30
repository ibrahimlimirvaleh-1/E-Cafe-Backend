using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ECafe.Migrator.Seeders;

internal static class DevTestMenuInventoryRecipeSeeder
{
    public static async Task SeedAsync(ECafeDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync("""
            WITH target_restaurants AS (
                SELECT DISTINCT i.restaurant_id
                FROM catalog.items i
                WHERE i.name IN (
                    'Americano',
                    'Cappuccino',
                    'Ice Latte',
                    'Latte',
                    'Limonlu çay',
                    'Meyvə çayı',
                    'Mocha',
                    'Nanəli çay',
                    'Qara çay',
                    'Yaşıl çay'
                )
                AND i."IsDeleted" = false
            ),
            stock_specs(name, unit_code, quantity_on_hand, low_stock_threshold) AS (
                VALUES
                    ('Espresso', 'ml', 5000.000000, 500.000000),
                    ('İsti su', 'ml', 20000.000000, 2000.000000),
                    ('Süd', 'ml', 10000.000000, 1000.000000),
                    ('Süd köpüyü', 'ml', 5000.000000, 500.000000),
                    ('Buz', 'g', 5000.000000, 500.000000),
                    ('Qara çay dəmi', 'g', 1000.000000, 100.000000),
                    ('Limon', 'pcs', 50.000000, 5.000000),
                    ('Şəkər', 'g', 3000.000000, 300.000000),
                    ('Meyvə qarışığı', 'g', 2000.000000, 200.000000),
                    ('Bal', 'g', 2000.000000, 200.000000),
                    ('Kakao', 'g', 1500.000000, 150.000000),
                    ('Şokolad siropu', 'ml', 3000.000000, 300.000000),
                    ('Nanə', 'g', 500.000000, 50.000000),
                    ('Yaşıl çay yarpağı', 'g', 1000.000000, 100.000000)
            )
            INSERT INTO inventory.inventory_items (
                restaurant_id,
                name,
                unit_id,
                quantity_on_hand,
                low_stock_threshold,
                is_active,
                "CreatedAt",
                "CreatedBy",
                "IsDeleted"
            )
            SELECT
                r.restaurant_id,
                s.name,
                u.id,
                s.quantity_on_hand,
                s.low_stock_threshold,
                true,
                NOW(),
                'dev-test-seed',
                false
            FROM target_restaurants r
            CROSS JOIN stock_specs s
            JOIN inventory.units u ON u.code = s.unit_code
            ON CONFLICT (restaurant_id, name) DO NOTHING;

            WITH recipe_specs(menu_item_name, stock_item_name, quantity, unit_code) AS (
                VALUES
                    ('Americano', 'Espresso', 30.000000, 'ml'),
                    ('Americano', 'İsti su', 120.000000, 'ml'),
                    ('Cappuccino', 'Espresso', 30.000000, 'ml'),
                    ('Cappuccino', 'Süd', 120.000000, 'ml'),
                    ('Cappuccino', 'Süd köpüyü', 60.000000, 'ml'),
                    ('Ice Latte', 'Espresso', 30.000000, 'ml'),
                    ('Ice Latte', 'Süd', 150.000000, 'ml'),
                    ('Ice Latte', 'Buz', 80.000000, 'g'),
                    ('Latte', 'Espresso', 30.000000, 'ml'),
                    ('Latte', 'Süd', 180.000000, 'ml'),
                    ('Latte', 'Süd köpüyü', 30.000000, 'ml'),
                    ('Limonlu çay', 'Qara çay dəmi', 5.000000, 'g'),
                    ('Limonlu çay', 'İsti su', 220.000000, 'ml'),
                    ('Limonlu çay', 'Limon', 1.000000, 'pcs'),
                    ('Limonlu çay', 'Şəkər', 8.000000, 'g'),
                    ('Meyvə çayı', 'Meyvə qarışığı', 12.000000, 'g'),
                    ('Meyvə çayı', 'İsti su', 220.000000, 'ml'),
                    ('Meyvə çayı', 'Bal', 10.000000, 'g'),
                    ('Mocha', 'Espresso', 30.000000, 'ml'),
                    ('Mocha', 'Süd', 160.000000, 'ml'),
                    ('Mocha', 'Kakao', 8.000000, 'g'),
                    ('Mocha', 'Şokolad siropu', 20.000000, 'ml'),
                    ('Nanəli çay', 'Qara çay dəmi', 5.000000, 'g'),
                    ('Nanəli çay', 'Nanə', 2.000000, 'g'),
                    ('Nanəli çay', 'İsti su', 220.000000, 'ml'),
                    ('Qara çay', 'Qara çay dəmi', 5.000000, 'g'),
                    ('Qara çay', 'İsti su', 220.000000, 'ml'),
                    ('Yaşıl çay', 'Yaşıl çay yarpağı', 5.000000, 'g'),
                    ('Yaşıl çay', 'İsti su', 220.000000, 'ml')
            )
            INSERT INTO inventory.recipes (
                restaurant_id,
                item_id,
                inventory_item_id,
                quantity,
                unit_id,
                is_active,
                "CreatedAt",
                "CreatedBy",
                "IsDeleted"
            )
            SELECT
                i.restaurant_id,
                i.id,
                stock.id,
                rs.quantity,
                u.id,
                true,
                NOW(),
                'dev-test-seed',
                false
            FROM recipe_specs rs
            JOIN catalog.items i
                ON i.name = rs.menu_item_name
                AND i."IsDeleted" = false
            JOIN inventory.inventory_items stock
                ON stock.restaurant_id = i.restaurant_id
                AND stock.name = rs.stock_item_name
                AND stock."IsDeleted" = false
            JOIN inventory.units u ON u.code = rs.unit_code
            ON CONFLICT (restaurant_id, item_id, inventory_item_id) DO NOTHING;
            """);
    }
}
