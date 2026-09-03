using VoiceKassa.Application.Interfaces;

namespace VoiceKassa.AiServices;

public class GeminiQueryService : IAiQueryService
{
    private readonly GeminiApiClient _client;

    private const string SystemPrompt = """
        Sen restoran yoki do'kon uchun AI hisobchisan. Senga JSON formatda
        savdo/buyurtma ma'lumotlari beriladi. Qoidalar:
        1. Har doim faqat RUS TILIDA javob ber. Savol o'zbekcha, ruscha yoki boshqa
           tilda kelganidan qat'i nazar, javob HAR DOIM rus tilida bo'lishi SHART.
           O'zbek tilida yoki boshqa tillarda hech qachon javob berma.
        2. Oddiy holatda javobni oddiy matn ko'rinishida yoz: **, *, #, ` va boshqa
           markdown belgilarni ishlatma. LEKIN agar foydalanuvchi aniq jadval so'rasa
           (masalan: "jadval shaklida chiqar", "выведи таблицу", "таблицей"), bunday
           ma'lumotni to'g'ri MARKDOWN JADVAL ko'rinishida ber:
           | Nomi | Turi | Summa |
           |------|------|-------|
           Jadval ichida ham ** yoki * belgilar ishlatma, hujayralar faqat toza matn bo'lsin.
        4. Agar savolga javob berish uchun ma'lumot yetarli bo'lmasa, rus tilida aniq shuni ayt.
        """;

    public GeminiQueryService(GeminiApiClient client) => _client = client;

    public async Task<string> AnswerAsync(string question, string dataContextJson, CancellationToken ct = default)
    {
        var userMessage = $"Savdo ma'lumotlari (JSON):\n{dataContextJson}\n\nSavol: {question}";
        var answer = await _client.CompleteAsync(SystemPrompt, userMessage, maxTokens: 500, ct: ct);
        // Xavfsizlik: javob rus tilida bo'lmasa (kirill harflari yo'q bo'lsa) —
        // bir marta qattiq ko'rsatma bilan qayta so'raymiz.
        if (!string.IsNullOrWhiteSpace(answer) && !HasCyrillic(answer))
        {
            var retryMessage = userMessage +
                "\n\nВАЖНО: Твой предыдущий ответ был не на русском языке." +
                " Отвечай ТОЛЬКО на русском языке, обычным текстом без markdown-разметки.";
            answer = await _client.CompleteAsync(SystemPrompt, retryMessage, maxTokens: 500, ct: ct);
        }
        return string.IsNullOrWhiteSpace(answer) ? "Javob topilmadi." : answer;
    }

    private const string SuperAdminSystemPrompt = """
        Sen VoiceKassa platformasining Super Admin AI yordamchisisan. Qoidalar:
        1. Har doim faqat RUS TILIDA javob ber. Savol o'zbekcha, ruscha yoki boshqa
           tilda berilganidan qat'i nazar, javob HAR DOIM rus tilida bo'lishi SHART.
           O'zbek tilida yoki boshqa tillarda hech qachon javob berma.
        2. Oddiy holatda javobni oddiy matn ko'rinishida yoz: **, *, #, ` va boshqa
           markdown belgilarni ishlatma. LEKIN agar foydalanuvchi aniq jadval so'rasa
           (masalan: "jadval shaklida chiqar", "выведи таблицу", "таблицей"), bunday
           ma'lumotni to'g'ri MARKDOWN JADVAL ko'rinishida ber:
           | Nomi | Turi | Summa |
           |------|------|-------|
           Jadval ichida ham ** yoki * belgilar ishlatma, hujayralar faqat toza matn bo'lsin.
        3. Senga barcha restoran/supermarket/do'konlar va ularning obuna holati JSON
           formatda beriladi. Faqat shu ma'lumotlar asosida qisqa va aniq javob ber.
           Hech qanday raqamni o'zing o'ylab topma yoki taxmin qilma.
        4. Agar savolga javob berish uchun ma'lumot yetarli bo'lmasa, rus tilida aniq shuni ayt.
        """;

    public async Task<string> AnswerPlatformAsync(string question, string dataContextJson, CancellationToken ct = default)
    {
        var userMessage = $"Platformadagi bizneslar (JSON):\n{dataContextJson}\n\nSavol: {question}";
        var answer = await _client.CompleteAsync(SuperAdminSystemPrompt, userMessage, maxTokens: 500, ct: ct);
        // Xavfsizlik: javob rus tilida bo'lmasa (kirill harflari yo'q bo'lsa) —
        // bir marta qattiq ko'rsatma bilan qayta so'raymiz.
        if (!string.IsNullOrWhiteSpace(answer) && !HasCyrillic(answer))
        {
            var retryMessage = userMessage +
                "\n\nВАЖНО: Твой предыдущий ответ был не на русском языке." +
                " Отвечай ТОЛЬКО на русском языке, обычным текстом без markdown-разметки.";
            answer = await _client.CompleteAsync(SuperAdminSystemPrompt, retryMessage, maxTokens: 500, ct: ct);
        }
        return string.IsNullOrWhiteSpace(answer) ? "Javob topilmadi." : answer;
    }

    private static bool HasCyrillic(string text)
    {
        foreach (var ch in text)
            if (ch is >= '\u0400' and <= '\u04FF')
                return true;
        return false;
    }
}
