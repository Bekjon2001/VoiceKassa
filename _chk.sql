\echo === LATEST 15 ORDERS (all businesses) ===
SELECT "ID","BUSINESS_ID","TABLE_ID","STATUS","TOTAL_AMOUNT","CREATED_AT"
FROM voicekassa."ORDERS"
ORDER BY "ID" DESC LIMIT 15;

\echo === ORDERS per business per status ===
SELECT "BUSINESS_ID","STATUS",COUNT(*) AS cnt, MAX("CREATED_AT") AS latest
FROM voicekassa."ORDERS"
GROUP BY "BUSINESS_ID","STATUS"
ORDER BY "BUSINESS_ID","STATUS";

\echo === business 44 (demo) orders detail ===
SELECT "ID","BUSINESS_ID","TABLE_ID","STATUS","CREATED_AT"
FROM voicekassa."ORDERS" WHERE "BUSINESS_ID"=44 ORDER BY "ID" DESC LIMIT 10;

\echo === business 44 tables + status ===
SELECT "ID","NAME","STATUS" FROM voicekassa."TABLES" WHERE "BUSINESS_ID"=44 ORDER BY "ID" LIMIT 25;
