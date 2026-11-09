-- ============================================================
-- VoiceKassa — Bron (RESERVATIONS) tizimi.
-- Jadval voicekassa.RESERVATIONS (loyiha ildizidagi skript).
-- STATUS: 0=Kutilmoqda, 1=Kelgan, 2=Yopilgan, 3=Bekor.
-- ============================================================

CREATE TABLE IF NOT EXISTS voicekassa.RESERVATIONS (
    ID              BIGSERIAL PRIMARY KEY,
    BUSINESS_ID     BIGINT      NOT NULL REFERENCES voicekassa.BUSINESSES (ID),
    TABLE_ID        BIGINT      NOT NULL REFERENCES voicekassa.TABLES (ID),
    BOOKING_DATE    DATE        NOT NULL,
    START_TIME      TIME        NULL,
    END_TIME        TIME        NULL,
    CUSTOMER_NAME   VARCHAR(100) NOT NULL,
    CUSTOMER_PHONE  VARCHAR(20)  NOT NULL,
    GUEST_COUNT     INT         NOT NULL,
    CODE            VARCHAR(6)   NOT NULL,
    SESSION_TOKEN   VARCHAR(64)  NULL,
    STATUS          INT         NOT NULL DEFAULT 0,
    CREATED_AT      TIMESTAMP    NOT NULL DEFAULT NOW()
);

-- Bir stolga bir sanada faqat bitta bron (UNIQUE(BUSINESS_ID, TABLE_ID, BOOKING_DATE)).
CREATE UNIQUE INDEX IF NOT EXISTS UX_RESERVATIONS_BUSINESS_TABLE_DATE
    ON voicekassa.RESERVATIONS (BUSINESS_ID, TABLE_ID, BOOKING_DATE);

-- 6 raqamli bron kodi butun tizimda yagona (UNIQUE(CODE)).
CREATE UNIQUE INDEX IF NOT EXISTS UX_RESERVATIONS_CODE
    ON voicekassa.RESERVATIONS (CODE);

-- ORDERS jadvaliga bron bog'lanishi.
ALTER TABLE voicekassa.ORDERS
    ADD COLUMN IF NOT EXISTS RESERVATION_ID BIGINT NULL;

-- Migratsiya yo'q: mavjud bazaga ham GUEST_COUNT ustuni qo'shilishi uchun.
ALTER TABLE voicekassa.RESERVATIONS
    ADD COLUMN IF NOT EXISTS GUEST_COUNT INT NOT NULL DEFAULT 0;