using System.Text.Json;
using System.Text.Json.Serialization;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;

namespace VoiceKassa.AiServices;

public class GeminiExtractionService : IAiExtractionService
{
    private readonly GeminiApiClient _client;

    private const string SystemPrompt = """
        Sen restoran yoki do'kon xodimi (kassir, ofitsiant) aytgan gapni
        strukturaviy JSON ga aylantirasan. Faqat JSON qaytar, hech qanday
        qo'shimcha matn, izoh yoki markdown belgisi yozma.

        Format:
        {"items":[{"name":"mahsulot/taom nomi","quantity":son,"unit":"dona|kg|litr|porsiya"}],"total":son yoki null,"paymentTypeRaw":"naqd|karta|onlayn|noaniq"}

        Qoidalar:
        - Raqamlarni so'zdan sonlarga aylantir ("olti ming" -> 6000, "ikkita" -> 2).
        - Agar jami summa alohida aytilmagan bo'lsa, "total": null qoyib ket (backend o'zi hisoblaydi).
        - Agar to'lov turi aytilmagan bo'lsa "paymentTypeRaw":"noaniq" qoyib ket.
        - Agar gapda hech qanday mahsulot/taom topa olmasang, "items": [] qaytar.
        """;

    public GeminiExtractionService(GeminiApiClient client) => _client = client;

    public async Task<OrderExtractionResult> ExtractOrderAsync(string transcriptText, CancellationToken ct = default)
    {
        try
        {
            var raw = await _client.CompleteAsync(SystemPrompt, transcriptText, maxTokens: 800, ct: ct);
            var json = StripCodeFence(raw);
            var parsed = JsonSerializer.Deserialize<RawExtraction>(json, JsonOpts);

            if (parsed is null || parsed.Items.Count == 0)
                return new OrderExtractionResult { Success = false, ErrorMessage = "Mahsulot/taom topilmadi." };

            return new OrderExtractionResult
            {
                Success = true,
                Total = parsed.Total,
                PaymentTypeRaw = parsed.PaymentTypeRaw ?? "noaniq",
                Items = parsed.Items.Select(i => new ExtractedItem
                {
                    Name = i.Name,
                    Quantity = i.Quantity,
                    Unit = string.IsNullOrWhiteSpace(i.Unit) ? "dona" : i.Unit,
                    Price = i.Price,
                }).ToList(),
            };
        }
        catch (Exception ex)
        {
            // Extraction failures should surface as a clean "couldn't
            // understand" message to the staff member, not a 500 error.
            return new OrderExtractionResult { Success = false, ErrorMessage = $"AI javobini tushunib bo'lmadi: {ex.Message}" };
        }
    }

    private static string StripCodeFence(string s) =>
        s.Replace("```json", "", StringComparison.OrdinalIgnoreCase)
         .Replace("```", "")
         .Trim();

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private class RawExtraction
    {
        [JsonPropertyName("items")] public List<RawItem> Items { get; set; } = new();
        [JsonPropertyName("total")] public decimal? Total { get; set; }
        [JsonPropertyName("paymentTypeRaw")] public string? PaymentTypeRaw { get; set; }
    }

    private class RawItem
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("quantity")] public decimal Quantity { get; set; }
        [JsonPropertyName("unit")] public string Unit { get; set; } = "dona";
        [JsonPropertyName("price")] public decimal? Price { get; set; }
    }
}
