\echo === ALL OPEN/INPROGRESS ORDERS (business, table, status, items, created) ===
SELECT o."ID", o."BUSINESS_ID", o."TABLE_ID", o."STATUS", o."TOTAL_AMOUNT",
       o."CREATED_AT", b."NAME" AS biznes, t."NAME" AS stol
FROM voicekassa."ORDERS" o
LEFT JOIN voicekassa."BUSINESSES" b ON b."ID" = o."BUSINESS_ID"
LEFT JOIN voicekassa."TABLES" t ON t."ID" = o."TABLE_ID"
WHERE o."STATUS" IN (0,1)
ORDER BY o."BUSINESS_ID", o."ID";

\echo === BUSINESSES (id, name, type) ===
SELECT "ID","NAME","TYPE" FROM voicekassa."BUSINESSES" ORDER BY "ID";

\echo === LATEST 10 ORDERS (any status) ===
SELECT o."ID", o."BUSINESS_ID", o."TABLE_ID", o."STATUS", o."CREATED_AT"
FROM voicekassa."ORDERS" o ORDER BY o."ID" DESC LIMIT 10;

SELECT column_name, data_type FROM information_schema.columns WHERE table_schema='voicekassa' AND table_name='ORDERS' ORDER BY ordinal_position;
\echo === recent orders ===
SELECT "ID","BUSINESS_ID","TABLE_ID","STATUS","TOTAL_AMOUNT","CREATED_AT" FROM voicekassa."ORDERS" ORDER BY "ID" DESC LIMIT 15;
