\echo === SO'NGGI 12 BUYURTMA (businessId, tableId, status, createdAt) ===
SELECT "ID","BUSINESS_ID","TABLE_ID","STATUS","TOTAL_AMOUNT","CREATED_AT"
FROM voicekassa."ORDERS"
ORDER BY "ID" DESC LIMIT 12;

\echo === OCHIQ (status=0/1) BUYURTMALAR businessId BO'YICHA ===
SELECT "BUSINESS_ID", "STATUS", COUNT(*)
FROM voicekassa."ORDERS"
WHERE "STATUS" IN (0,1)
GROUP BY "BUSINESS_ID","STATUS"
ORDER BY "BUSINESS_ID";

\echo === BUSINESS nomlari (id -> name, type) ===
SELECT "ID","NAME","TYPE" FROM voicekassa."BUSINESSES" ORDER BY "ID";
