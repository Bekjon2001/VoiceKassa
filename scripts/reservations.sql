-- ============================================================
-- VoiceKassa — Bron (RESERVATIONS) tizimi. Migratsiya yo'q:
-- bu skript qo'lda (psql yoki DBeaver) bir marta bajariladi.
-- Sana: 2026-10-15
-- ============================================================

-- 1) Yangi jadval: voicekassa.RESERVATIONS
--    STATUS: 0=Kutilmoqda, 1=Kelgan, 2=Yopilgan, 3=Bekor
--    START_TIME/END_TIME null bo'lsa — bron butun kun uchun.
CREATE TABLE IF NOT EXISTS voicekassa.RESERVATIONS (
    ID              BIGSERIAL PRIMARY KEY,
    BUSINESS_ID     BIGINT      NOT NULL REFERENCES voicekassa.BUSINESSES (ID),
    TABLE_ID        BIGINT      NOT NULL REFERENCES voicekassa.TABLES (ID),
    BOOKING_DATE    DATE        NOT NULL,
    START_TIME      TIME        NULL,
    END_TIME        TIME        NULL,
    CUSTOMER_NAME   VARCHAR(200) NULL,
    CUSTOMER_PHONE  VARCHAR(30)  NULL,
    CODE            VARCHAR(6)  NOT NULL,
    SESSION_TOKEN   VARCHAR(200) NULL,
    STATUS          SMALLINT    NOT NULL DEFAULT 0,
    CREATED_AT      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Bir stolga bir sanada faqat bitta bron (spec: UNIQUE(BUSINESS_ID, TABLE_ID, BOOKING_DATE)).
-- Eslatma: yopilgan/bekor qilingan bron ham shu sanani band qilaveradi — kun
-- ichida takroriy bron kerak bo'lsa, shu indeks qisman (partial) qilinadi.
CREATE UNIQUE INDEX IF NOT EXISTS UX_RESERVATIONS_BUSINESS_TABLE_DATE
    ON voicekassa.RESERVATIONS (BUSINESS_ID, TABLE_ID, BOOKING_DATE);

-- 6 raqamli bron kodi butun tizimda yagona.
CREATE UNIQUE INDEX IF NOT EXISTS UX_RESERVATIONS_CODE
    ON voicekassa.RESERVATIONS (CODE);

-- Kod bilan kirish (enter) tez topilsin.
CREATE INDEX IF NOT EXISTS IX_RESERVATIONS_CODE_LOOKUP
    ON voicekassa.RESERVATIONS (CODE, STATUS);

-- 2) ORDERS jadvaliga bron bog'lanishi (mijoz buyurtmasi qaysi bronga tegishli).
ALTER TABLE voicekassa.ORDERS
    ADD COLUMN IF NOT EXISTS RESERVATION_ID BIGINT NULL;

CREATE INDEX IF NOT EXISTS IX_ORDERS_RESERVATION_ID
    ON voicekassa.ORDERS (RESERVATION_ID);