-- ============================================================
-- VoiceKassa — Auth jadvallari (RESTAURANT_OWNERS, USER_ACCOUNTS)
-- Bu qo'shimcha skript, mavjud "voicekassa" schema'siga 2 ta yangi
-- jadval qo'shadi. Boshqa jadvallarga (BUSINESSES, PRODUCTS va h.k.)
-- tegmaydi.
--
-- Ishga tushirish: pgAdmin'da Query Tool orqali, bir marta.
-- ============================================================

-- ------------------------------------------------------------
-- USER_ACCOUNTS — platforma darajasidagi akkauntlar (Super Admin)
-- ------------------------------------------------------------
DROP TABLE IF EXISTS voicekassa."USER_ACCOUNTS";

CREATE TABLE voicekassa."USER_ACCOUNTS" (
    "ID"              bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "BUSINESS_ID"     bigint NULL,
    "FULL_NAME"       varchar(200) NOT NULL,
    "PHONE_NUMBER"    varchar(30) NULL,
    "LOGIN"           varchar(100) NOT NULL,
    "PASSWORD_HASH"   text NOT NULL,
    "IS_ACTIVE"       boolean NOT NULL DEFAULT true,
    "IS_SUPER_ADMIN"  boolean NOT NULL DEFAULT false,
    "ACCESS_TOKEN"    varchar(200) NULL,
    "CREATED_AT"      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "FK_USERACCOUNTS_BUSINESSES" FOREIGN KEY ("BUSINESS_ID")
        REFERENCES voicekassa."BUSINESSES" ("ID") ON DELETE SET NULL
);

CREATE UNIQUE INDEX "IX_USERACCOUNTS_LOGIN" ON voicekassa."USER_ACCOUNTS" ("LOGIN");

-- ------------------------------------------------------------
-- RESTAURANT_OWNERS — har bir restoranning egasi + obuna/to'lov
-- ------------------------------------------------------------
DROP TABLE IF EXISTS voicekassa."RESTAURANT_OWNERS";

CREATE TABLE voicekassa."RESTAURANT_OWNERS" (
    "ID"                     bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "BUSINESS_ID"            bigint NOT NULL,
    "FULL_NAME"              varchar(200) NOT NULL,
    "PHONE_NUMBER"           varchar(30) NOT NULL,
    "LOGIN"                  varchar(100) NOT NULL,
    "PASSWORD_HASH"          text NOT NULL,
    "SUBSCRIPTION_AMOUNT"    numeric(18,2) NOT NULL DEFAULT 0,
    "PAYMENT_PAID_AT"        timestamptz NOT NULL,
    "SUBSCRIPTION_MONTHS"    integer NOT NULL DEFAULT 1,
    "SUBSCRIPTION_ENDS_AT"   timestamptz NOT NULL,
    "ACCESS_TOKEN"           varchar(200) NULL,
    "IS_ACTIVE"              boolean NOT NULL DEFAULT true,
    "CREATED_AT"             timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "FK_RESTOWNERS_BUSINESSES" FOREIGN KEY ("BUSINESS_ID")
        REFERENCES voicekassa."BUSINESSES" ("ID") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_RESTOWNERS_LOGIN" ON voicekassa."RESTAURANT_OWNERS" ("LOGIN");
CREATE UNIQUE INDEX "IX_RESTOWNERS_BUSINESS" ON voicekassa."RESTAURANT_OWNERS" ("BUSINESS_ID");

-- ============================================================
-- Tayyor. Tekshirish uchun:
-- SELECT table_name FROM information_schema.tables WHERE table_schema = 'voicekassa';
-- ============================================================
