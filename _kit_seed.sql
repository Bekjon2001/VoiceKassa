-- Ochiq buyurtmasi YO'Q restoranlarga 3 tadan ochiq buyurtma (status=0) yaratamiz.
-- Har buyurtma: Stol 1/2/3 da, 2 ta mahsulot bilan. Bog'liq stol "band" (status=1) bo'ladi.
BEGIN;

WITH rest AS (
  SELECT "ID" AS bid
  FROM voicekassa."BUSINESSES"
  WHERE "TYPE" = 0
    AND "ID" NOT IN (SELECT DISTINCT "BUSINESS_ID" FROM voicekassa."ORDERS" WHERE "STATUS" = 0)
),
tbl AS (
  SELECT "BUSINESS_ID", "ID" AS tid,
         ROW_NUMBER() OVER (PARTITION BY "BUSINESS_ID" ORDER BY "ID") AS rn
  FROM voicekassa."TABLES"
),
prod AS (
  SELECT "BUSINESS_ID", "ID" AS pid, "NAME", "PRICE", "UNIT",
         ROW_NUMBER() OVER (PARTITION BY "BUSINESS_ID" ORDER BY "ID") AS rn
  FROM voicekassa."PRODUCTS"
),
ins_orders AS (
  INSERT INTO voicekassa."ORDERS" ("BUSINESS_ID","TABLE_ID","STATUS","TOTAL_AMOUNT","PAYMENT_TYPE","TRANSCRIPT_TEXT","CREATED_AT")
  SELECT r.bid, t.tid, 0, 0, 0, 'Buyurtma (sinov)', now()
  FROM rest r
  JOIN tbl t ON t."BUSINESS_ID" = r.bid AND t.rn <= 3
  RETURNING "ID" AS oid, "BUSINESS_ID" AS bid
),
ins_items AS (
  INSERT INTO voicekassa."ORDER_ITEMS" ("ORDER_ID","PRODUCT_ID","PRODUCT_NAME_SPOKEN","QUANTITY","UNIT","LINE_TOTAL")
  SELECT o.oid, p.pid, p."NAME", 1, p."UNIT", p."PRICE"
  FROM ins_orders o
  JOIN prod p ON p."BUSINESS_ID" = o.bid AND p.rn <= 2
  RETURNING "ORDER_ID" AS oid, "LINE_TOTAL" AS lt
)
UPDATE voicekassa."ORDERS" o
SET "TOTAL_AMOUNT" = COALESCE((SELECT SUM(i."LINE_TOTAL") FROM voicekassa."ORDER_ITEMS" i WHERE i."ORDER_ID" = o."ID"), 0)
WHERE o."STATUS" = 0;

-- Ochiq buyurtmasi bor stollarni "band" qilamiz (mantiqan to'g'ri holat).
UPDATE voicekassa."TABLES" t
SET "STATUS" = 1
WHERE t."ID" IN (SELECT "TABLE_ID" FROM voicekassa."ORDERS" WHERE "STATUS" = 0);

COMMIT;

-- Tekshiruv: har bir restorandagi ochiq buyurtmalar soni
SELECT "BUSINESS_ID", COUNT(*) AS open_orders
FROM voicekassa."ORDERS"
WHERE "STATUS" IN (0,1)
GROUP BY "BUSINESS_ID"
ORDER BY "BUSINESS_ID";

