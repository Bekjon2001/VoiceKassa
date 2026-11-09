# VoiceKassa

Restoranlar / cafe-lar uchun ovvoz buyurtma (voice-order) va bron boshqaruv tizimi. MVP (minimal viable product) bosqichi.

## ⚠️ Xavfsizlik eslatmalari (MVP)

- **CORS:** `AllowAnyOrigin()` — barcha manbalar qabul qilindi. Faqat lokal rivojlanish / demo uchun. Production'ga chiqarishdan oldin real originlar ro'yxatiga o'tkazilishi shart.
- **Swagger:** `Development` ortida ochiq. Production'da yopiq bo'lishi kerak (`Program.cs` da `IsDevelopment()` tekshiruv bor).
- **DB paroli:** `appsettings.json` da saqlanmasligi kerak. `dotnet user-secrets` orqali sozlanadi (batafsil quyida).
- **Token:** standart JWT/[Authorize] o'rniga `X-Owner-Token` (va `X-Table-Token`) maxsus HTTP headerlar orqali autentifikatsiya amalga oshiriladi. Tokenlar Guid asosida tasodifiy generatsiya qilinadi va DB saqlanadi. Keyinchalik JWT'ga o'tish oson (faqat `AuthService` ichida o'zgartiriladi).

## Loyiha tuzilmasi

```
VoiceKassa/
├── src/
│   ├── VoiceKassa.Api/          # ASP.NET Core Web API (net8.0)
│   ├── VoiceKassa.Application/  # Biznes mantiq, DTO, interfeyslar
│   ├── VoiceKassa.Domain/       # Domain entitylar, enumlar
│   ├── VoiceKassa.DataLayer/    # EF Core DbContext
│   ├── VoiceKassa.DataLayer.Repository/
│   └── VoiceKassa.AiServices/   # Gemini AI klienti, Edge-TTS
├── frontend/                    # HTML/JS/CSS admin va mijoz panellar
├── scripts/sql/                 # DB sinov so'rovlari (_chk.sql va boshqalar)
├── tests/                       # Unit testlar (xUnit)
├── _start_server.cmd            # Serverni ishga tushiruvchi skript
├── swagger_order_paths.txt      # Swagger marshrutlarining tartibi
├── VoiceKassa.sln
├── .gitignore
└── README.md
```

## 🗄️ Ma'lumotlar bazasini sozlash (PostgreSQL)

`appsettings.json` da **parol yo'q** (xavfsizlik uchun o'chirilgan):

```json
"ConnectionStrings": {
  "Default": "Host=localhost;Port=5432;Database=voicekassa;Username=postgres;"
}
```

Parolni **User Secrets** orqali qo'shing (API kalitlari kabi maxfiy ma'lumotlar shu yerda saqlanadi):

```powershell
cd src\VoiceKassa.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=voicekassa;Username=postgres;Password=SIZNING_PAROLINGIZ"
```

Gemini AI kalitini ham shu qilish kerak:

```powershell
dotnet user-secrets set "Gemini:ApiKey" "SIZNING_GEMINI_KALITINGIZ"
```

## 🚀 Ishga tushirish

### 1. Server (API)

Variant A — `_start_server.cmd` orqali:

```cmd
_start_server.cmd
```

Variant B — `dotnet run` orqali:

```powershell
cd src\VoiceKassa.Api
dotnet run --environment Development --urls "http://localhost:55983"
```

Brauzerdan oching: `http://localhost:55983/` (index.html/admin paneli) yoki `http://localhost:55983/swagger` (Swagger UI, faqat Development).

### 2. Frontend

`frontend/` papkasidagi fayllar API server orqali(static files) taqdim etiladi:
- `index.html` / `menu.html` — mijoz sahifalari
- `admin.html` / `super-admin.html` — admin panellar

## 🔑 Autentifikatsiya (X-Owner-Token)

Standart JWT/[Authorize] o'rniga `X-Owner-Token` headeri ishlatiladi:

| Header | Maqsad | Kim foydalanadi |
|--------|--------|-----------------|
| `X-Owner-Token` | Restoran egasi / admin ruxsatini tekshirish | `frontend/admin.js`, `frontend/super-admin.js` |
| `X-Table-Token` | Mijozning o'z stoliga ulanish sessiyasi | `frontend/app.js` |

Misol (fetch):

```javascript
fetch("/api/Order/GetOpen/42", {
  headers: { "X-Owner-Token": ownerToken }
});
```

## 📜 SQL sinov so'rovlari

Ildizdagi `_chk.sql` va unga o'xshash 15 ta fayl `scripts/sql/` papkasiga yig'ilgan (jami 231 qator). Ular DB tekshiruv, havola (seed) va buyurtma/rezervatsiya so'rovlarini o'z ichiga oladi.

Fayllar ro'yxati:
- `_chk.sql`, `_dbcheck.sql`, `_kit.sql`, `_kit2.sql`, `_kit_check.sql`, `_kit_seed.sql`
- `_now.sql`, `_ord.sql`, `_ord_check.sql`, `_ord_final.sql`, `_ord_now.sql`
- `_recent.sql`, `_recent_orders.sql`, `_t44.sql`
- `reservations.sql`

## 🧪 Testlar

Unit testlar `tests/VoiceKassa.Api.Tests/` papkasida (xUnit). Baza ga bog'liq bo'lmagan servislar (masalan, `PasswordHasher`) sinab chiqiladi.

Testlarni ishga tushirish:

```powershell
dotnet test tests/VoiceKassa.Api.Tests/VoiceKassa.Api.Tests.csproj
```

## 🛠️ Texnik izohlar

- **Swagger** — faqat `Development` muhitida ochiq (`Program.cs`).
- **CORS** — `AllowAnyOrigin()`, `AllowAnyHeader()`, `AllowAnyMethod()` (MVP).
- **Token tekshiruvi** — har controller ichida `Request.Headers["X-Owner-Token"]` orqali, `BusinessService.AuthorizeOwnerAsync` yordamida.
- **Gemini AI** — buyurtma matnidan mahsulot/miqdorni ajratib olish uchun ishlatiladi (bepul tarif).

---

*MVP (Minimum Viable Product) — asosiy funksiyalar ishlaydi, lekin production uchun CORS, Swagger, DB parol saqlash va auth (JWT) ni tajrida yangilash kerak.*