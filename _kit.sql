\echo === ORDER columns ===
SELECT column_name, data_type FROM information_schema.columns
WHERE table_schema='voicekassa' AND table_name='ORDERS' ORDER BY ordinal_position;
\echo === ORDER_ITEMS columns ===
SELECT column_name, data_type FROM information_schema.columns
WHERE table_schema='voicekassa' AND table_name='ORDER_ITEMS' ORDER BY ordinal_position;
\echo === open/in-progress orders per business ===
SELECT "BUSINESS_ID","STATUS",COUNT(*) FROM voicekassa."ORDERS"
WHERE "STATUS" IN (0,1) GROUP BY "BUSINESS_ID","STATUS" ORDER BY "BUSINESS_ID";
\echo === all orders summary per business ===
SELECT "BUSINESS_ID","STATUS",COUNT(*),MAX("CREATED_AT") AS last FROM voicekassa."ORDERS"
GROUP BY "BUSINESS_ID","STATUS" ORDER BY "BUSINESS_ID";
\echo === restaurants (type 0) list ===
SELECT "ID","NAME","TYPE","IS_ACTIVE" FROM voicekassa."BUSINESSES" WHERE "TYPE"=0 ORDER BY "ID";
