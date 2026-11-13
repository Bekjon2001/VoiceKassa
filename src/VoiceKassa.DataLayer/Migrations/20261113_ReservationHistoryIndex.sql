DO $$
DECLARE
    old_index RECORD;
BEGIN
    FOR old_index IN
        SELECT ns.nspname AS schema_name, idx.relname AS index_name
        FROM pg_index i
        JOIN pg_class tbl ON tbl.oid = i.indrelid
        JOIN pg_namespace ns ON ns.oid = tbl.relnamespace
        JOIN pg_class idx ON idx.oid = i.indexrelid
        WHERE ns.nspname = 'voicekassa'
          AND tbl.relname = 'RESERVATIONS'
          AND i.indisunique
          AND i.indpred IS NULL
          AND (
              SELECT array_agg(lower(pg_get_indexdef(i.indexrelid, pos, true)) ORDER BY pos)
              FROM generate_series(1, i.indnkeyatts) AS pos
          ) = ARRAY['business_id', 'table_id', 'booking_date']
    LOOP
        EXECUTE format('DROP INDEX %I.%I', old_index.schema_name, old_index.index_name);
    END LOOP;
END
$$;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_RESERVATIONS_ACTIVE_TABLE_DAY"
    ON voicekassa."RESERVATIONS" ("BUSINESS_ID", "TABLE_ID", "BOOKING_DATE")
    WHERE "STATUS" IN (0, 1);
