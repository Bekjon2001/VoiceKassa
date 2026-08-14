using VoiceKassa.Application.Interfaces;

namespace VoiceKassa.AiServices;

public class GeminiQueryService : IAiQueryService
{
    private readonly GeminiApiClient _client;

    private const string SystemPrompt = """
        Sen do'kon uchun AI hisobchisan. Senga JSON formatda savdo ma'lumotlari beriladi.
        Faqat shu ma'lumotlar asosida, o'zbek tilida, qisqa va aniq javob ber.
        Hech qanday raqamni o'zing o'ylab topma yoki taxmin qilma - faqat berilgan JSON'dagi
        summalarni qo'shish, sanash yoki saralash orqali javob ber.
        Agar savolga javob berish uchun ma'lumot yetarli bo'lmasa, aniq shuni ayt.
        """;

    public GeminiQueryService(GeminiApiClient client) => _client = client;

    public async Task<string> AnswerAsync(string question, string dataContextJson, CancellationToken ct = default)
    {
        var userMessage = $"Savdo ma'lumotlari (JSON):\n{dataContextJson}\n\nSavol: {question}";
        var answer = await _client.CompleteAsync(SystemPrompt, userMessage, maxTokens: 500, ct: ct);
        return string.IsNullOrWhiteSpace(answer) ? "Javob topilmadi." : answer;
    }
}
