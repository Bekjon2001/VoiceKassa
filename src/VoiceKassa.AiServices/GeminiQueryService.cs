using System.Text.Json;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;

namespace VoiceKassa.AiServices;

public class GeminiQueryService : IAiQueryService
{
    private readonly GeminiApiClient _client;

    private const string SystemPrompt = """
        Sen restoran yoki do'kon uchun AI hisobchisan. Senga JSON formatda
        savdo/buyurtma ma'lumotlari beriladi. Qoidalar:
        1. Har doim faqat O'ZBEK TILIDA (lotin yozuvida) javob ber. Savol o'zbekcha,
           ruscha yoki boshqa tilda kelganidan qat'i nazar, javob HAR DOIM o'zbek
           tilida, lotin harflarida bo'lishi SHART. Rus yoki boshqa tillarda hech
           qachon javob berma.
        2. Oddiy holatda javobni oddiy matn ko'rinishida yoz: **, *, #, ` va boshqa
           markdown belgilarni ishlatma. LEKIN agar foydalanuvchi aniq jadval so'rasa
           (masalan: "jadval shaklida chiqar", "таблицей"), bunday
           ma'lumotni to'g'ri MARKDOWN JADVAL ko'rinishida ber:
           | Nomi | Turi | Summa |
           |------|------|-------|
           Jadval ichida ham ** yoki * belgilar ishlatma, hujayralar faqat toza matn bo'lsin.
        4. Agar savolga javob berish uchun ma'lumot yetarli bo'lmasa, o'zbek tilida aniq shuni ayt.
        """;

    public GeminiQueryService(GeminiApiClient client) => _client = client;

    public async Task<string> AnswerAsync(string question, string dataContextJson, CancellationToken ct = default)
    {
        var userMessage = $"Savdo ma'lumotlari (JSON):\n{dataContextJson}\n\nSavol: {question}";
        var answer = await _client.CompleteAsync(SystemPrompt, userMessage, maxTokens: 500, ct: ct);
        // Xavfsizlik: javob o'zbekcha (lotin) bo'lmasa — ya'ni kirill harflari
        // qolgan bo'lsa — bir marta qattiq ko'rsatma bilan qayta so'raymiz.
        if (!string.IsNullOrWhiteSpace(answer) && HasCyrillic(answer))
        {
            var retryMessage = userMessage +
                "\n\nMUHIM: Oldingi javobing o'zbek tilida emas edi." +
                " Faqat O'ZBEK TILIDA, LOTIN yozuvida, markdown belgilarsiz oddiy matnda javob ber.";
            answer = await _client.CompleteAsync(SystemPrompt, retryMessage, maxTokens: 500, ct: ct);
        }
        return string.IsNullOrWhiteSpace(answer) ? "Javob topilmadi." : answer;
    }

    private const string SuperAdminSystemPrompt = """
        Sen VoiceKassa platformasining Super Admin AI yordamchisisan. Qoidalar:
        1. Har doim faqat O'ZBEK TILIDA (lotin yozuvida) javob ber. Savol o'zbekcha,
           ruscha yoki boshqa tilda berilganidan qat'i nazar, javob HAR DOIM o'zbek
           tilida, lotin harflarida bo'lishi SHART. Rus yoki boshqa tillarda hech
           qachon javob berma.
        2. Oddiy holatda javobni oddiy matn ko'rinishida yoz: **, *, #, ` va boshqa
           markdown belgilarni ishlatma. LEKIN agar foydalanuvchi aniq jadval so'rasa
           (masalan: "jadval shaklida chiqar", "таблицей"), bunday
           ma'lumotni to'g'ri MARKDOWN JADVAL ko'rinishida ber:
           | Nomi | Turi | Summa |
           |------|------|-------|
           Jadval ichida ham ** yoki * belgilar ishlatma, hujayralar faqat toza matn bo'lsin.
        3. Senga barcha restoran/supermarket/do'konlar va ularning obuna holati JSON
           formatda beriladi. Faqat shu ma'lumotlar asosida qisqa va aniq javob ber.
           Hech qanday raqamni o'zing o'ylab topma yoki taxmin qilma.
        4. Agar savolga javob berish uchun ma'lumot yetarli bo'lmasa, o'zbek tilida aniq shuni ayt.
        """;

    public async Task<string> AnswerPlatformAsync(string question, string dataContextJson, CancellationToken ct = default)
    {
        var userMessage = $"Platformadagi bizneslar (JSON):\n{dataContextJson}\n\nSavol: {question}";
        var answer = await _client.CompleteAsync(SuperAdminSystemPrompt, userMessage, maxTokens: 500, ct: ct);
        // Xavfsizlik: javob o'zbekcha (lotin) bo'lmasa — ya'ni kirill harflari
        // qolgan bo'lsa — bir marta qattiq ko'rsatma bilan qayta so'raymiz.
        if (!string.IsNullOrWhiteSpace(answer) && HasCyrillic(answer))
        {
            var retryMessage = userMessage +
                "\n\nMUHIM: Oldingi javobing o'zbek tilida emas edi." +
                " Faqat O'ZBEK TILIDA, LOTIN yozuvida, markdown belgilarsiz oddiy matnda javob ber.";
            answer = await _client.CompleteAsync(SuperAdminSystemPrompt, retryMessage, maxTokens: 500, ct: ct);
        }
        return string.IsNullOrWhiteSpace(answer) ? "Javob topilmadi." : answer;
    }

    // ======================= Jarvis: buyruq/savol tahlili (function calling) =======================

    private const string JarvisSystemPrompt = """
        Sen VoiceKassa platformasining Jarvis yordamchisisan. Senga foydalanuvchi
        matni beriladi (ko'pincha ovoz tanib olishdan kelgan — imlo xatolari,
        buzilgan so'zlar bo'lishi mumkin) va platformadagi bizneslar JSON konteksti.
        Vazifang — matn BUYRUQmi yoki SAVOLmi aniqlash:
        1. BUYRUQ bo'lsa (bo'lim ochish, biznes tanlash, faollashtirish/passiv
           qilish) — mos funksiyani TO'G'RI parametrlar bilan chaqir:
           - businessId — FAQAT kontekstdagi Id maydonidan olinadi. Aytib
             berilgan biznes nomini o'zing id'ga moslashtirasan. Kontekstda
             yo'q id'ni hech qachon yozma.
           - Biznes nomi aytilmagan bo'lsa (masalan faqat "restoranlarni och")
             — navigate ishlatiladi.
           - "yangi restoran qo'sh / yaratish" — navigate(view=create),
             "yangi supermarket qo'sh" — navigate(view=market-create).
        2. SAVOL bo'lsa — funksiya chaqirmasdan, faqat O'ZBEK TILIDA (lotin
           yozuvida), qisqa va aniq javob ber. Javob FAQAT kontekstdagi haqiqiy
           ma'lumotlarga asoslanadi; hisoblash yoki filtrlash kerak bo'lsa
           (oyma-oy, jami, o'rtacha, "6 oydan katta" kabi) — kontekstdan o'zing
           hisobla, "aniqlashtiring" deb qaytarma. Foydalanuvchi jadval so'rasa
           MARKDOWN JADVAL ber (| Nomi | Muddati | ... |), jadval kataklarida
           markdown belgi ishlatma. Ma'lumot yetarli bo'lmasa — o'zbek tilida
           aniq shuni ayt.
        3. Na buyruq, na savol bo'lgan oddiy gap bo'lsa — o'zbek tilida qisqa
           munosabat bildir yoki nima qilishni so'rang.
        4. Javob matni HAR DOIM o'zbek tilida (lotin yozuvida) bo'lsin — rus yoki
           boshqa tillarda hech qachon javob berma.
        """;

    private static readonly HashSet<string> JarvisKnownActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "navigate", "select_business", "activate_business", "deactivate_business",
    };

    public async Task<JarvisCommandResponse> InterpretJarvisAsync(string text, string dataContextJson, CancellationToken ct = default)
    {
        var userMessage = $"Platformadagi bizneslar (JSON):\n{dataContextJson}\n\nFoydalanuvchi matni: {text}";
        var result = await _client.CompleteWithToolsAsync(JarvisSystemPrompt, userMessage, BuildJarvisTools(), maxTokens: 500, ct);

        // Model buyruq deb topdi — funksiya chaqiruvini amalga aylantiramiz.
        if (result.ToolCall != null)
        {
            var action = result.ToolCall.Name;
            if (!JarvisKnownActions.Contains(action))
            {
                return new JarvisCommandResponse
                {
                    Kind = "answer",
                    Answer = "Kechirasiz, bunday buyruqni tushunmadim. Boshqacha ayting.",
                };
            }

            string? view = null;
            long? businessId = null;
            try
            {
                using var doc = JsonDocument.Parse(result.ToolCall.ArgsJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("view", out var viewEl)) view = viewEl.GetString();
                    businessId = ExtractBusinessId(doc.RootElement);
                }
            }
            catch { /* args buzilgan — null qoladi, frontend xato beradi */ }

            return new JarvisCommandResponse
            {
                Kind = "action",
                Action = action.ToLowerInvariant(),
                View = view,
                BusinessId = businessId,
            };
        }

        // Model savol deb topdi — matn javob.
        var answer = (result.Text ?? "").Trim();
        // Xavfsizlik: javob o'zbekcha (lotin) bo'lmasa — kirill harflari qolgan
        // bo'lsa — bir marta qattiq ko'rsatma bilan qayta so'raymiz.
        if (!string.IsNullOrWhiteSpace(answer) && HasCyrillic(answer))
        {
            var retryMessage = userMessage +
                "\n\nMUHIM: Oldingi javobing o'zbek tilida emas edi." +
                " Faqat O'ZBEK TILIDA, LOTIN yozuvida, oddiy matnda javob ber" +
                " (foydalanuvchi jadval so'ragan bo'lsa — markdown jadval mumkin).";
            var retry = await _client.CompleteWithToolsAsync(JarvisSystemPrompt, retryMessage, BuildJarvisTools(), maxTokens: 500, ct);
            var retryAnswer = (retry.Text ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(retryAnswer) && !HasCyrillic(retryAnswer)) answer = retryAnswer;
        }
        return new JarvisCommandResponse
        {
            Kind = "answer",
            Answer = string.IsNullOrWhiteSpace(answer) ? "Javob topilmadi." : answer,
        };
    }

    private static long? ExtractBusinessId(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object) return null;
        if (!args.TryGetProperty("businessId", out var el)) return null;
        if (el.ValueKind == JsonValueKind.Number)
        {
            if (el.TryGetInt64(out var l)) return l;
            if (el.TryGetDouble(out var d)) return (long)Math.Round(d);
            return null;
        }
        if (el.ValueKind == JsonValueKind.String && long.TryParse(el.GetString(), out var parsed)) return parsed;
        return null;
    }

    // Jarvis panel amallari (tools) — yangi amal qo'shish uchun shu ro'yxatni
    // kengaytirish va frontend'dagi executeJarvisAction'ga mos handler qo'shish kifoya.
    private static List<GeminiFunctionDeclaration> BuildJarvisTools()
    {
        var views = new[] { "restaurants", "markets", "create", "market-create", "system", "ai" };

        var navigateParams = new Dictionary<string, object>
        {
            ["type"] = "OBJECT",
            ["properties"] = new Dictionary<string, object>
            {
                ["view"] = new Dictionary<string, object>
                {
                    ["type"] = "STRING",
                    ["enum"] = views,
                    ["description"] = "Ochiladigan bo'lim: restaurants (restoranlar ro'yxati), markets (supermarketlar), create (yangi restoran formasi), market-create (yangi supermarket formasi), system (tizim sozlamalari), ai (AI chat)",
                },
            },
            ["required"] = new[] { "view" },
        };

        var businessParams = new Dictionary<string, object>
        {
            ["type"] = "OBJECT",
            ["properties"] = new Dictionary<string, object>
            {
                ["businessId"] = new Dictionary<string, object>
                {
                    ["type"] = "INTEGER",
                    ["description"] = "Kontekstdagi biznes Id si",
                },
            },
            ["required"] = new[] { "businessId" },
        };

        return new List<GeminiFunctionDeclaration>
        {
            new() { Name = "navigate", Description = "Paneldagi bo'limni ochish", Parameters = navigateParams },
            new() { Name = "select_business", Description = "Ro'yxatda shu biznesni tanlash va batafsil ma'lumotini ochish", Parameters = businessParams },
            new() { Name = "activate_business", Description = "Biznes egasini (obunani) faollashtirish", Parameters = businessParams },
            new() { Name = "deactivate_business", Description = "Biznes egasini (obunani) passivlashtirish", Parameters = businessParams },
        };
    }

    private static bool HasCyrillic(string text)
    {
        foreach (var ch in text)
            if (ch is >= '\u0400' and <= '\u04FF')
                return true;
        return false;
    }
}
