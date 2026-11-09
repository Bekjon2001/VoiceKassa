\echo === tables per restaurant ===
SELECT "BUSINESS_ID", COUNT(*) AS tables FROM voicekassa."TABLES" GROUP BY "BUSINESS_ID" ORDER BY "BUSINESS_ID";
\echo === products per restaurant ===
SELECT "BUSINESS_ID", COUNT(*) AS products FROM voicekassa."PRODUCTS" GROUP BY "BUSINESS_ID" ORDER BY "BUSINESS_ID";
\echo === sample table ids for business 1,8,44 ===
SELECT "BUSINESS_ID","ID","NAME" FROM voicekassa."TABLES" WHERE "BUSINESS_ID" IN (1,8,44) ORDER BY "BUSINESS_ID","ID" LIMIT 20;
\echo === sample product ids for business 1,8,44 ===
SELECT "BUSINESS_ID","ID","NAME","CATEGORY_ID" FROM voicekassa."PRODUCTS" WHERE "BUSINESS_ID" IN (1,8,44) ORDER BY "BUSINESS_ID","ID" LIMIT 20;
\echo === business 44 tables ===
SELECT "ID","BUSINESS_ID","NAME","STATUS" FROM voicekassa."TABLES" WHERE "BUSINESS_ID"=44 ORDER BY "ID";
