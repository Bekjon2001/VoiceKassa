-- ============================================================
-- VoiceKassa — Xodimlar bo'limi uchun bazani yangilash (STAFF + maosh tarixi)
--
-- DIQQAT: Bu skriptni pgAdmin'da, "voicekassa" schema'siga bir marta ishga tushiring.
-- Migratsiya/EF emas — ustunlar qo'shish va yangi jadval yaratish, mavjud
-- ma'lumotlar BUZILMAYDI. FULL_NAME ustuni saqlanib qoladi.
-- ============================================================

-- ------------------------------------------------------------
-- 1) STAFF jadvaliga yangi ustunlar qo'shish
--    (Oldin: FULL_NAME, PHONE_NUMBER, ROLE, IS_ACTIVE, CREATED_AT)
-- ------------------------------------------------------------
ALTER TABLE voicekassa."STAFF"
    ADD COLUMN IF NOT EXISTS "FIRST_NAME"     varchar(200) NULL,
    ADD COLUMN IF NOT EXISTS "LAST_NAME"      varchar(200) NULL,
    ADD COLUMN IF NOT EXISTS "AGE"            integer NULL,
    ADD COLUMN IF NOT EXISTS "MONTHLY_SALARY" numeric(18,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "HIRE_DATE"      timestamptz NULL,
    ADD COLUMN IF NOT EXISTS "FIRED_AT"       timestamptz NULL;

-- ------------------------------------------------------------
-- 2) Maosh tarixi jadvali
--    Sana | Eski maosh | Yangi maosh | Sabab/izoh
-- ------------------------------------------------------------
CREATE TABLE IF NOT EXISTS voicekassa."STAFF_SALARY_HISTORY" (
    "ID"          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "STAFF_ID"    bigint NOT NULL,
    "CHANGED_AT"  timestamptz NOT NULL DEFAULT now(),
    "OLD_SALARY"  numeric(18,2) NOT NULL DEFAULT 0,
    "NEW_SALARY"  numeric(18,2) NOT NULL DEFAULT 0,
    "REASON"      varchar(300) NULL,
    CONSTRAINT "FK_SALHIST_STAFF" FOREIGN KEY ("STAFF_ID")
        REFERENCES voicekassa."STAFF" ("ID") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_SALHIST_STAFF" ON voicekassa."STAFF_SALARY_HISTORY" ("STAFF_ID");

-- ------------------------------------------------------------
-- Tayyor. Tekshirish:
-- SELECT column_name FROM information_schema.columns
--   WHERE table_schema='voicekassa' AND table_name='STAFF';
-- ============================================================
