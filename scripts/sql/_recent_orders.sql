-- 1) 73-buyurtma tarkibi qaysi mahsulotga tegishli va u qaysi biznesniki
SELECT oi."ORDER_ID", oi."PRODUCT_ID", oi."PRODUCT_NAME_SPOKEN", p."BUSINESS_ID" AS prod_biz, p."NAME"
FROM voicekassa."ORDER_ITEMS" oi
LEFT JOIN voicekassa."PRODUCTS" p ON p."ID" = oi."PRODUCT_ID"
WHERE oi."ORDER_ID" = 73;

-- 2) business 44 mahsulotlari
SELECT "ID", "NAME", "PRICE" FROM voicekassa."PRODUCTS" WHERE "BUSINESS_ID" = 44 LIMIT 15;

-- 3) business 8 (Anor) stollari
SELECT "ID", "NAME", "STATUS" FROM voicekassa."TABLES" WHERE "BUSINESS_ID" = 8 ORDER BY "ID" LIMIT 15;

-- 4) business 8 eng oxirgi buyurtmalari
SELECT "ID", "TABLE_ID", "STATUS", "TOTAL_AMOUNT", "CREATED_AT"
FROM voicekassa."ORDERS" WHERE "BUSINESS_ID" = 8 ORDER BY "ID" DESC LIMIT 5;
