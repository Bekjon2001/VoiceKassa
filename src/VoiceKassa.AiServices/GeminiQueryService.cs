using VoiceKassa.Application.Interfaces;

namespace VoiceKassa.AiServices;

public class GeminiQueryService : IAiQueryService
{
    private readonly GeminiApiClient _client;

    private const string SystemPrompt = """
        Sen restoran yoki do'kon uchun AI hisobchisan. Senga JSON formatda
        savdo/buyurtma ma'lumotlari beriladi. Faqat shu ma'lumotlar asosida,
        o'zbek tilida, qisqa va aniq javob ber. Hech qanday raqamni o'zing
        o'ylab topma yoki taxmin qilma - faqat berilgan JSON'dagi summalarni
        qo'shish, sanash yoki saralash orqali javob ber.
        Agar savolga javob berish uchun ma'lumot yetarli bo'lmasa, aniq shuni ayt.
        """;

    public GeminiQueryService(GeminiApiClient client) => _client = client;

    public async Task<string> AnswerAsync(string question, string dataContextJson, CancellationToken ct = default)
    {
        var userMessage = $"Savdo ma'lumotlari (JSON):\n{dataContextJson}\n\nSavol: {question}";
        var answer = await _client.CompleteAsync(SystemPrompt, userMessage, maxTokens: 500, ct: ct);
        return string.IsNullOrWhiteSpace(answer) ? "Javob topilmadi." : answer;
    }

    private const string SuperAdminSystemPrompt = """
        Sen VoiceKassa platformasining Super Admin AI yordamchisisan.
        Senga barcha restoran/supermarket/do'konlar va ularning obuna holati JSON
        formatda beriladi. Faqat shu ma'lumotlar asosida, o'zbek tilida, qisqa va
        aniq javob ber. Hech qanday raqamni o'zing o'ylab topma yoki taxmin qilma.
        Agar savolga javob berish uchun ma'lumot yetarli bo'lmasa, aniq shuni ayting.
        """;

    public async Task<string> AnswerPlatformAsync(string question, string dataContextJson, CancellationToken ct = default)
    {
        var userMessage = $"Platformadagi bizneslar (JSON):\n{dataContextJson}\n\nSavol: {question}";
        var answer = await _client.CompleteAsync(SuperAdminSystemPrompt, userMessage, maxTokens: 500, ct: ct);
        return string.IsNullOrWhiteSpace(answer) ? "Javob topilmadi." : answer;
    }
}
