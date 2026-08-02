-- ECafe local/demo seed data.
-- Purpose: fill existing active restaurants with demo tables, menu, inventory, recipes and staff.
-- Safe to run more than once. Do not use this script for production data.

DO $$
DECLARE
    restaurant_row record;

    category_soups_id int;
    category_main_id int;
    category_drinks_id int;

    item_dushbara_id int;
    item_chicken_id int;
    item_cola_id int;

    inv_flour_id int;
    inv_meat_id int;
    inv_onion_id int;
    inv_chicken_id int;
    inv_rice_id int;
    inv_cola_id int;

    waiter_user_id int;
    kitchen_user_id int;
    restaurant_slug text;
    demo_group_id int;

    -- BCrypt hash for password: password
    demo_password_hash text := '$2a$10$7EqJtq98hPqEX7fNZaFWoOhi8WCdOVG/xE6LofoLFHo9lYuBlypLe';
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM core.restaurants
        WHERE is_active = true
          AND "IsDeleted" = false
    ) THEN
        INSERT INTO core.restaurant_groups
            (name, legal_name, is_active, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            ('Demo Group', 'Demo Group LLC', true, now(), 'demo-seed', false)
        ON CONFLICT (name)
        DO UPDATE SET legal_name = EXCLUDED.legal_name, is_active = true
        RETURNING id INTO demo_group_id;

        INSERT INTO core.restaurants
            (name, location, phone, email, restaurant_group_id, branch_name, rating_average, rating_count, deposit_amount, cancellation_window_minutes, service_fee_percent, staff_settlement_period, is_active, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            ('Demo Cafe Merkezi', 'Baku, Nizami', '+994120000001', 'demo.cafe@ecafe.demo', demo_group_id, 'Merkezi filial', 4.70, 18, 10.00, 30, 3.00, 7, true, now(), 'demo-seed', false)
        ON CONFLICT (email)
        DO UPDATE SET is_active = true, restaurant_group_id = EXCLUDED.restaurant_group_id;
    END IF;

    FOR restaurant_row IN
        SELECT id, name
        FROM core.restaurants
        WHERE is_active = true
          AND "IsDeleted" = false
        ORDER BY id
    LOOP
        restaurant_slug := regexp_replace(lower(restaurant_row.name), '[^a-z0-9]+', '', 'g');
        IF restaurant_slug = '' THEN
            restaurant_slug := 'restaurant' || restaurant_row.id;
        END IF;

        INSERT INTO ops.tables
            (restaurant_id, table_no, name, capacity, is_active, is_empty, "CreatedAt", "CreatedBy", "IsDeleted")
        SELECT restaurant_row.id, v.table_no, v.name, v.capacity, true, true, now(), 'demo-seed', false
        FROM (VALUES
            (1, 'Masa 1', 2),
            (2, 'Masa 2', 4),
            (3, 'Masa 3', 4),
            (4, 'VIP Masa', 6),
            (5, 'Aile masasi', 8)
        ) AS v(table_no, name, capacity)
        ON CONFLICT (restaurant_id, table_no) DO NOTHING;

        INSERT INTO catalog.categories
            (restaurant_id, name, slug, sort_order, is_active, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            (restaurant_row.id, 'Shorbalar', 'sorbalar', 1, true, now(), 'demo-seed', false)
        ON CONFLICT (restaurant_id, slug)
        DO UPDATE SET name = EXCLUDED.name
        RETURNING id INTO category_soups_id;

        INSERT INTO catalog.categories
            (restaurant_id, name, slug, sort_order, is_active, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            (restaurant_row.id, 'Esas yemekler', 'esas-yemekler', 2, true, now(), 'demo-seed', false)
        ON CONFLICT (restaurant_id, slug)
        DO UPDATE SET name = EXCLUDED.name
        RETURNING id INTO category_main_id;

        INSERT INTO catalog.categories
            (restaurant_id, name, slug, sort_order, is_active, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            (restaurant_row.id, 'Ichkiler', 'ickiler', 3, true, now(), 'demo-seed', false)
        ON CONFLICT (restaurant_id, slug)
        DO UPDATE SET name = EXCLUDED.name
        RETURNING id INTO category_drinks_id;

        INSERT INTO catalog.items
            (restaurant_id, category_id, status_id, name, description, base_price, is_available, sales_count, is_active, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            (restaurant_row.id, category_soups_id, 5001, 'Dushbere', 'Xirda xemir, qiyme, sogan ve nane ile hazirlanir.', 4.50, true, 0, true, now(), 'demo-seed', false)
        ON CONFLICT (category_id, name)
        DO UPDATE SET description = EXCLUDED.description, base_price = EXCLUDED.base_price, is_available = true, is_active = true
        RETURNING id INTO item_dushbara_id;

        INSERT INTO catalog.items
            (restaurant_id, category_id, status_id, name, description, base_price, is_available, sales_count, is_active, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            (restaurant_row.id, category_main_id, 5001, 'Toyuq plov', 'Toyuq eti, duyu ve terevezlerle porsiya yemeyi.', 8.90, true, 0, true, now(), 'demo-seed', false)
        ON CONFLICT (category_id, name)
        DO UPDATE SET description = EXCLUDED.description, base_price = EXCLUDED.base_price, is_available = true, is_active = true
        RETURNING id INTO item_chicken_id;

        INSERT INTO catalog.items
            (restaurant_id, category_id, status_id, name, description, base_price, is_available, sales_count, is_active, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            (restaurant_row.id, category_drinks_id, 5001, 'Coca-Cola 330 ml', 'Soyuq ichki.', 2.50, true, 0, true, now(), 'demo-seed', false)
        ON CONFLICT (category_id, name)
        DO UPDATE SET description = EXCLUDED.description, base_price = EXCLUDED.base_price, is_available = true, is_active = true
        RETURNING id INTO item_cola_id;

        INSERT INTO inventory.inventory_items
            (restaurant_id, name, unit_id, quantity_on_hand, low_stock_threshold, is_active, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            (restaurant_row.id, 'Un', 2, 5000, 500, true, now(), 'demo-seed', false)
        ON CONFLICT (restaurant_id, name)
        DO UPDATE SET quantity_on_hand = GREATEST(inventory.inventory_items.quantity_on_hand, EXCLUDED.quantity_on_hand), low_stock_threshold = EXCLUDED.low_stock_threshold, is_active = true
        RETURNING id INTO inv_flour_id;

        INSERT INTO inventory.inventory_items
            (restaurant_id, name, unit_id, quantity_on_hand, low_stock_threshold, is_active, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            (restaurant_row.id, 'Qiyme', 2, 3000, 400, true, now(), 'demo-seed', false)
        ON CONFLICT (restaurant_id, name)
        DO UPDATE SET quantity_on_hand = GREATEST(inventory.inventory_items.quantity_on_hand, EXCLUDED.quantity_on_hand), low_stock_threshold = EXCLUDED.low_stock_threshold, is_active = true
        RETURNING id INTO inv_meat_id;

        INSERT INTO inventory.inventory_items
            (restaurant_id, name, unit_id, quantity_on_hand, low_stock_threshold, is_active, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            (restaurant_row.id, 'Sogan', 2, 2000, 300, true, now(), 'demo-seed', false)
        ON CONFLICT (restaurant_id, name)
        DO UPDATE SET quantity_on_hand = GREATEST(inventory.inventory_items.quantity_on_hand, EXCLUDED.quantity_on_hand), low_stock_threshold = EXCLUDED.low_stock_threshold, is_active = true
        RETURNING id INTO inv_onion_id;

        INSERT INTO inventory.inventory_items
            (restaurant_id, name, unit_id, quantity_on_hand, low_stock_threshold, is_active, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            (restaurant_row.id, 'Toyuq eti', 2, 6000, 800, true, now(), 'demo-seed', false)
        ON CONFLICT (restaurant_id, name)
        DO UPDATE SET quantity_on_hand = GREATEST(inventory.inventory_items.quantity_on_hand, EXCLUDED.quantity_on_hand), low_stock_threshold = EXCLUDED.low_stock_threshold, is_active = true
        RETURNING id INTO inv_chicken_id;

        INSERT INTO inventory.inventory_items
            (restaurant_id, name, unit_id, quantity_on_hand, low_stock_threshold, is_active, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            (restaurant_row.id, 'Duyu', 2, 7000, 1000, true, now(), 'demo-seed', false)
        ON CONFLICT (restaurant_id, name)
        DO UPDATE SET quantity_on_hand = GREATEST(inventory.inventory_items.quantity_on_hand, EXCLUDED.quantity_on_hand), low_stock_threshold = EXCLUDED.low_stock_threshold, is_active = true
        RETURNING id INTO inv_rice_id;

        INSERT INTO inventory.inventory_items
            (restaurant_id, name, unit_id, quantity_on_hand, low_stock_threshold, is_active, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            (restaurant_row.id, 'Coca-Cola 330 ml', 5, 48, 10, true, now(), 'demo-seed', false)
        ON CONFLICT (restaurant_id, name)
        DO UPDATE SET quantity_on_hand = GREATEST(inventory.inventory_items.quantity_on_hand, EXCLUDED.quantity_on_hand), low_stock_threshold = EXCLUDED.low_stock_threshold, is_active = true
        RETURNING id INTO inv_cola_id;

        INSERT INTO inventory.recipes
            (restaurant_id, item_id, inventory_item_id, quantity, unit_id, is_active, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            (restaurant_row.id, item_dushbara_id, inv_flour_id, 120, 2, true, now(), 'demo-seed', false),
            (restaurant_row.id, item_dushbara_id, inv_meat_id, 80, 2, true, now(), 'demo-seed', false),
            (restaurant_row.id, item_dushbara_id, inv_onion_id, 20, 2, true, now(), 'demo-seed', false),
            (restaurant_row.id, item_chicken_id, inv_chicken_id, 180, 2, true, now(), 'demo-seed', false),
            (restaurant_row.id, item_chicken_id, inv_rice_id, 160, 2, true, now(), 'demo-seed', false),
            (restaurant_row.id, item_cola_id, inv_cola_id, 1, 5, true, now(), 'demo-seed', false)
        ON CONFLICT (restaurant_id, item_id, inventory_item_id)
        DO UPDATE SET quantity = EXCLUDED.quantity, unit_id = EXCLUDED.unit_id, is_active = true;

        INSERT INTO auth.users
            (name, surname, email, phone, password, is_active, role_id, rating, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            ('Demo', 'Ofisiant', 'waiter.' || restaurant_row.id || '.' || restaurant_slug || '@ecafe.demo', '+99477000' || lpad(restaurant_row.id::text, 4, '0'), demo_password_hash, true, 4, 4.80, now(), 'demo-seed', false)
        ON CONFLICT (email)
        DO UPDATE SET name = EXCLUDED.name, surname = EXCLUDED.surname, is_active = true, role_id = 4, rating = EXCLUDED.rating
        RETURNING id INTO waiter_user_id;

        INSERT INTO auth.user_restaurants
            (user_id, restaurant_id, is_active, service_fee_percent, "Id", "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            (waiter_user_id, restaurant_row.id, true, 3.00, COALESCE((SELECT MAX("Id") + 1 FROM auth.user_restaurants), 1), now(), 'demo-seed', false)
        ON CONFLICT (user_id, restaurant_id)
        DO UPDATE SET is_active = true, service_fee_percent = EXCLUDED.service_fee_percent;

        INSERT INTO auth.users
            (name, surname, email, phone, password, is_active, role_id, rating, "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            ('Demo', 'Metbex', 'kitchen.' || restaurant_row.id || '.' || restaurant_slug || '@ecafe.demo', '+99478000' || lpad(restaurant_row.id::text, 4, '0'), demo_password_hash, true, 6, 4.90, now(), 'demo-seed', false)
        ON CONFLICT (email)
        DO UPDATE SET name = EXCLUDED.name, surname = EXCLUDED.surname, is_active = true, role_id = 6, rating = EXCLUDED.rating
        RETURNING id INTO kitchen_user_id;

        INSERT INTO auth.user_restaurants
            (user_id, restaurant_id, is_active, service_fee_percent, "Id", "CreatedAt", "CreatedBy", "IsDeleted")
        VALUES
            (kitchen_user_id, restaurant_row.id, true, null, COALESCE((SELECT MAX("Id") + 1 FROM auth.user_restaurants), 1), now(), 'demo-seed', false)
        ON CONFLICT (user_id, restaurant_id)
        DO UPDATE SET is_active = true, service_fee_percent = EXCLUDED.service_fee_percent;
    END LOOP;
END $$;
