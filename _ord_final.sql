\echo === Hozirgi ochiq buyurtmalar (business, status, sana) ===
SELECT o."ID", o."BUSINESS_ID", o."TABLE_ID", o."STATUS", o."CREATED_AT", b."NAME" AS business_name
FROM voicekassa."ORDERS" o
LEFT JOIN voicekassa."BUSINESSES" b ON b."ID" = o."BUSINESS_ID"
ORDER BY o."ID" DESC LIMIT 30;

\echo === Har bir biznesdagi ochiq (0) va jarayonda (1) buyurtmalar soni ===
SELECT o."BUSINESS_ID", b."NAME", b."TYPE", COUNT(*) FILTER (WHERE o."STATUS" IN (0,1)) AS open_cnt
FROM voicekassa."ORDERS" o
LEFT JOIN voicekassa."BUSINESSES" b ON b."ID" = o."BUSINESS_ID"
GROUP BY o."BUSINESS_ID", b."NAME", b."TYPE"
ORDER BY open_cnt DESC, o."BUSINESS_ID";
