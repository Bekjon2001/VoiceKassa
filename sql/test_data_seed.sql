-- ============================================================
-- VoiceKassa — Test ma'lumotlari: MAVJUD RESTORANLAR uchun
-- (pgAdmin Query Tool yoki psql orqali ishga tushiring)
--
-- Har bir RESTORAN (BUSINESSES.TYPE = 0) uchun:
--   1) "Taomlar"/"Ichimliklar" kategoriyalari (mavjud bo'lsa olib qoladi)
--   2) 100 ta xodim    (STAFF)
--   3) 100 ta taom     (PRODUCTS, ALIASES='food')
--   4) 100 ta ichimlik (PRODUCTS, ALIASES='drink')
--
-- XUSUSIYATLARI:
--   * Yangi biznes yaratilmaydi — faqat bazadagi mavjud restoranlar to'ldiriladi.
--   * Marketlar (TYPE = 1) ga tegilmaydi.
--   * Qayta ishga tushirilsa dubl bo'lmaydi: avval shu restoranning xodimlari
--     va mahsulotlari o'chirilib qaytadan to'ldiriladi (ORDER_ITEMS da
--     ishlatilgan mahsulotlar FK sabab saqlanib qoladi).
--   * "ID" ustunlari liniyada UMUMAN YO'Q — ular DB'da
--     GENERATED ALWAYS AS IDENTITY (avto-raqamlaydi), qo'lda yozilmaydi.
-- ============================================================

DO $$
DECLARE
    v_rest          record;
    v_food_cat_id   bigint;
    v_drink_cat_id  bigint;
    v_cnt_staff     bigint;
    v_cnt_food      bigint;
    v_cnt_drink     bigint;
BEGIN
    FOR v_rest IN
        SELECT "ID", "NAME"
        FROM voicekassa."BUSINESSES"
        WHERE "TYPE" = 0 AND "IS_ACTIVE" = true
        ORDER BY "ID"
    LOOP
        -- 1) Kategoriyalar
        SELECT "ID" INTO v_food_cat_id
        FROM voicekassa."CATEGORIES"
        WHERE "BUSINESS_ID" = v_rest."ID" AND "NAME" = 'Taomlar'
        LIMIT 1;
        IF v_food_cat_id IS NULL THEN
            INSERT INTO voicekassa."CATEGORIES" ("BUSINESS_ID", "NAME", "SORT_ORDER")
            VALUES (v_rest."ID", 'Taomlar', 1)
            RETURNING "ID" INTO v_food_cat_id;
        END IF;

        SELECT "ID" INTO v_drink_cat_id
        FROM voicekassa."CATEGORIES"
        WHERE "BUSINESS_ID" = v_rest."ID" AND "NAME" = 'Ichimliklar'
        LIMIT 1;
        IF v_drink_cat_id IS NULL THEN
            INSERT INTO voicekassa."CATEGORIES" ("BUSINESS_ID", "NAME", "SORT_ORDER")
            VALUES (v_rest."ID", 'Ichimliklar', 2)
            RETURNING "ID" INTO v_drink_cat_id;
        END IF;

        -- 2) Eski test yozuvlarini tozalash (dubl oldini oladi).
        --    ORDER_ITEMS da ishlatilgan mahsulotlar FK sabab o'chirilmaydi.
        DELETE FROM voicekassa."PRODUCTS" p
        WHERE p."BUSINESS_ID" = v_rest."ID"
          AND NOT EXISTS (
              SELECT 1 FROM voicekassa."ORDER_ITEMS" oi WHERE oi."PRODUCT_ID" = p."ID");
        DELETE FROM voicekassa."STAFF" s WHERE s."BUSINESS_ID" = v_rest."ID";

        -- 3) 100 ta xodim — ROLE: 0=Owner 1=Manager 2=Cashier 3=Waiter 4=Cook
        INSERT INTO voicekassa."STAFF" (
            "BUSINESS_ID", "FIRST_NAME", "LAST_NAME", "FULL_NAME", "PHONE_NUMBER",
            "ROLE", "AGE", "MONTHLY_SALARY", "HIRE_DATE", "IS_ACTIVE", "CREATED_AT")
        SELECT
            v_rest."ID", n.first_name, n.last_name,
            n.first_name || ' ' || n.last_name,
            '+998' || (900000000 + v_rest."ID" * 1000000 + i * 1111)::text,
            (i % 5)::int, 20 + (i % 25),
            2500000 + (i % 20) * 150000,
            now() - ((i % 365) || ' days')::interval, true, now()
        FROM generate_series(1, 100) AS i
        CROSS JOIN LATERAL (
            SELECT
                (ARRAY['Aziz','Bekzod','Dilshod','Eldor','Farhod','G''ani','Hasan',
                       'Islom','Jasur','Kamol','Laziz','Murod','Nodir','Otabek',
                       'Popul','Rustam','Sardor','Temur','Ulug''bek','Vohid'])[(i % 20) + 1] AS first_name,
                (ARRAY['Aliyev','Karimov','Rahimov','Sattorov','Toshmatov','Umarov',
                       'Xolmatov','Yusupov','Zokirov','Nazarov','Qodirov','Mamatqulov',
                       'Ergashev','Jo''rayev','Turg''unov','Sobirov','Sharipov',
                       'Ismoilov','G''ulomov','Haydarov','Ochilov','Normatov',
                       'Xasanov','Burxonov','Salimov'])[((i * 7) % 25) + 1] AS last_name
        ) AS n;
        GET DIAGNOSTICS v_cnt_staff = ROW_COUNT;



        -- 4) 100 ta taom — ALIASES='food' (admin panel bo'limni shu belgi bilan aniqlaydi)
        INSERT INTO voicekassa."PRODUCTS" (
            "BUSINESS_ID", "CATEGORY_ID", "NAME", "ALIASES", "UNIT", "PRICE",
            "LOW_STOCK_THRESHOLD", "IS_AVAILABLE", "CREATED_AT", "UPDATED_AT")
        SELECT
            v_rest."ID", v_food_cat_id, n.dish || ' ' || i, 'food', 'dona',
            15000 + ((i * 7) % 30) * 2500, 0, true, now(), now()
        FROM generate_series(1, 100) AS i
        CROSS JOIN LATERAL (
            SELECT (ARRAY['Lag''mon','Manti','Norin','Shashlik','Osh','Somsa',
                          'Chuchvara','Qovurdoq','Guruch','Baliq','Kabob','Dimlama',
                          'Kartoshka fri','Pitza','Burger','Hot-dog','Achchiq salat',
                          'Sho''rva','Qaynatma','Tovuq qanoti','Qozon-kabob','Beshbarmoq',
                          'Ma''suv','Qovurilgan baliq','Tuxum li'])[(i % 25) + 1] AS dish
        ) AS n;
        GET DIAGNOSTICS v_cnt_food = ROW_COUNT;


        -- 5) 100 ta ichimlik — ALIASES='drink'
        INSERT INTO voicekassa."PRODUCTS" (
            "BUSINESS_ID", "CATEGORY_ID", "NAME", "ALIASES", "UNIT", "PRICE",
            "LOW_STOCK_THRESHOLD", "IS_AVAILABLE", "CREATED_AT", "UPDATED_AT")
        SELECT
            v_rest."ID", v_drink_cat_id, n.drink || ' ' || i, 'drink', 'dona',
            4000 + ((i * 3) % 20) * 1500, 0, true, now(), now()
        FROM generate_series(1, 100) AS i
        CROSS JOIN LATERAL (
            SELECT (ARRAY['Coca-Cola','Pepsi','Fanta','Sprite','Ayron','Kompot',
                          'Qora choy','Ko''k choy','Limonad','Ko''k suv','Mineral suv',
                          'Qahva','Moxito','Anor sharbati','Olma sharbati','Uzum sharbati',
                          'Energy','Kefir','Sut','Sut kokteyl'])[(i % 20) + 1] AS drink
        ) AS n;
        GET DIAGNOSTICS v_cnt_drink = ROW_COUNT;

        RAISE NOTICE 'Restoran "%": xodim=%, taom=%, ichimlik=%',
            v_rest."NAME", v_cnt_staff, v_cnt_food, v_cnt_drink;
    END LOOP;
END $$;

-- ============================================================
-- Tekshirish so'rovlari (skriptdan keyin ishga tushiring)
-- ============================================================
SELECT b."ID", b."NAME",
       (SELECT count(*) FROM voicekassa."STAFF" s WHERE s."BUSINESS_ID" = b."ID") AS staff,
       (SELECT count(*) FROM voicekassa."PRODUCTS" p WHERE p."BUSINESS_ID" = b."ID" AND p."ALIASES" = 'food') AS food,
       (SELECT count(*) FROM voicekassa."PRODUCTS" p WHERE p."BUSINESS_ID" = b."ID" AND p."ALIASES" = 'drink') AS drink
FROM voicekassa."BUSINESSES" b
WHERE b."TYPE" = 0 AND b."IS_ACTIVE" = true
ORDER BY b."ID";

SELECT "ALIASES", count(*) AS total FROM voicekassa."PRODUCTS" GROUP BY "ALIASES";
SELECT count(*) AS staff_total FROM voicekassa."STAFF";
SELECT b."NAME", c."NAME" AS cat, count(p."ID") AS n
FROM voicekassa."CATEGORIES" c
JOIN voicekassa."BUSINESSES" b ON b."ID" = c."BUSINESS_ID"
LEFT JOIN voicekassa."PRODUCTS" p ON p."CATEGORY_ID" = c."ID"
WHERE b."TYPE" = 0
GROUP BY b."NAME", c."NAME"
ORDER BY b."NAME", c."NAME"
LIMIT 6;
