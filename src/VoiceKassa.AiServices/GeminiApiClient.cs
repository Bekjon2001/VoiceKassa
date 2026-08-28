using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoiceKassa.AiServices;

public class GeminiApiOptions
{
    public string ApiKey { get; set; } = string.Empty;

    // "gemini-2.0-flash" - Google AI Studio'ning bepul tarifida ishlaydigan
    // tezkor model. Kerak bo'lsa appsettings.json orqali almashtiriladi.
    public string Model { get; set; } = "gemini-2.0-flash";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models";
}

/// <summary>
/// Thin wrapper around Gemini's generateContent endpoint. Both the
/// extraction service and the query service reuse this instead of
/// talking to HttpClient directly.
/// Bepul kalitni https://aistudio.google.com/apikey sahifasidan olish mumkin.
/// </summary>
public class GeminiApiClient
{
    private readonly HttpClient _http;
    private readonly GeminiApiOptions _options;

    public GeminiApiClient(HttpClient http, GeminiApiOptions options)
    {
        _http = http;
        _options = options;
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userMessage, int maxTokens = 1000, CancellationToken ct = default)
    {
        // 1) Belgilangan model ro'yxati bo'yicha urinamiz.
        var models = new List<string> { _options.Model, "gemini-2.5-flash", "gemini-2.0-flash", "gemini-1.5-flash" };

        string? lastError = null;
        foreach (var model in models)
        {
            var text = await TryModelAsync(model, systemPrompt, userMessage, maxTokens, ct);
            if (text != null) return text;
            // TryModelAsync xatolik haqida xabar qaytarmaydi — 404/400'da next.
        }

        // 2) Hech biri ishlamasa — Google'ning ListModels API'sidan
        //    generateContent qo'llab-quvvatlanadigan modelni dinamik topamiz.
        var auto = await TryAutoModelAsync(systemPrompt, userMessage, maxTokens, ct);
        if (auto != null) return auto;

        throw new HttpRequestException($"Gemini so'rovi bajarilmadi: barcha modellar mavjud emas yoki ruxsat etilmagan. {lastError ?? ""}");
    }

    /// <summary>Berilgan modelga urinadi; muvaffaqiyat bo'lsa matn, aks holda null qaytaradi.</summary>
    private async Task<string?> TryModelAsync(string model, string systemPrompt, string userMessage, int maxTokens, CancellationToken ct)
    {
        try
        {
            var url = $"{_options.BaseUrl}/{model}:generateContent?key={_options.ApiKey}";
            var payload = BuildPayload(systemPrompt, userMessage, maxTokens);
            var response = await _http.PostAsJsonAsync(url, payload, ct);
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: ct);
            var text = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            return string.IsNullOrWhiteSpace(text) ? null : text!;
        }
        catch { return null; }
    }

    /// <summary>Google ListModels'dan generateContent'ga mos joriy modelni topib sinaydi.</summary>
    private async Task<string?> TryAutoModelAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct)
    {
        try
        {
            var listUrl = $"{_options.BaseUrl}?key={_options.ApiKey}";
            var listResp = await _http.GetAsync(listUrl, ct);
            if (!listResp.IsSuccessStatusCode) return null;

            var listRaw = await listResp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(listRaw);
            var candidates = new List<string>();

            if (doc.RootElement.TryGetProperty("models", out var modelsArr))
            {
                foreach (var m in modelsArr.EnumerateArray())
                {
                    var name = m.TryGetProperty("name", out var n) ? n.GetString() : null;
                    bool supports = false;
                    if (m.TryGetProperty("supportedGenerationMethods", out var methods))
                    {
                        foreach (var mv in methods.EnumerateArray())
                            if (mv.GetString() == "generateContent") { supports = true; break; }
                    }
                    if (!supports || string.IsNullOrEmpty(name)) continue;

                    // Model ichidan "models/" prefiksi va turi:
                    var shortName = name.Contains('/') ? name.Substring(name.LastIndexOf('/') + 1) : name;
                    if (shortName.Contains("thinking")) continue; // faqat tezkor javob modellarini xohlaymiz
                    candidates.Add(shortName);
                }
            }

            // Eng yaxshi tanlovlar: flaflash/light tarafdagi modellarni oldinga chiqaramiz.
            candidates.Sort((a, b) => ScoreModel(a).CompareTo(ScoreModel(b)));

            foreach (var candidate in candidates)
            {
                var text = await TryModelAsync(candidate, systemPrompt, userMessage, maxTokens, ct);
                if (text != null) return text;
            }
        }
        catch { /* ignore */ }
        return null;
    }

    private static int ScoreModel(string name)
    {
        // Kichikroq = yaxshiroq tanlov (tezkor flash modellarni birinchi o'ringa).
        if (name.Contains("flash")) return 0;
        if (name.Contains("lite")) return 1;
        return 2;
    }

    private static GeminiRequest BuildPayload(string systemPrompt, string userMessage, int maxTokens)
    {
        return new GeminiRequest
        {
            SystemInstruction = new GeminiContent
            {
                Parts = new List<GeminiPart> { new() { Text = systemPrompt } },
            },
            Contents = new List<GeminiContent>
            {
                new() { Role = "user", Parts = new List<GeminiPart> { new() { Text = userMessage } } },
            },
            GenerationConfig = new GeminiGenerationConfig
            {
                MaxOutputTokens = maxTokens,
                Temperature = 0,
            },
        };
    }

    private class GeminiRequest
    {
        [JsonPropertyName("system_instruction")] public GeminiContent SystemInstruction { get; set; } = new();
        [JsonPropertyName("contents")] public List<GeminiContent> Contents { get; set; } = new();
        [JsonPropertyName("generationConfig")] public GeminiGenerationConfig GenerationConfig { get; set; } = new();
    }

    private class GeminiGenerationConfig
    {
        [JsonPropertyName("maxOutputTokens")] public int MaxOutputTokens { get; set; }
        [JsonPropertyName("temperature")] public double Temperature { get; set; }
    }

    private class GeminiContent
    {
        [JsonPropertyName("role")] public string? Role { get; set; }
        [JsonPropertyName("parts")] public List<GeminiPart> Parts { get; set; } = new();
    }

    private class GeminiPart
    {
        [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    }

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")] public List<GeminiCandidate>? Candidates { get; set; }
    }

    private class GeminiCandidate
    {
        [JsonPropertyName("content")] public GeminiContent? Content { get; set; }
    }
}
