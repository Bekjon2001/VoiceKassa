using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace VoiceKassa.AiServices;

public class GeminiApiOptions
{
    public string ApiKey { get; set; } = string.Empty;

    // Boshlang'ich model. Auto-detect har doim ishlaydi (ListModels'dan
    // eng yaxshi flash modelni topadi), shuning uchun bu yerda aniq model
    // ko'rsatish shart emas — bo'sh qoldirish ham mumkin.
    public string Model { get; set; } = "";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models";
}

/// <summary>Gemini qaytargan funksiya chaqiruvi (buyruq amali).</summary>
public class GeminiToolCall
{
    public string Name { get; init; } = string.Empty;
    public string ArgsJson { get; init; } = "{}";
}

/// <summary>
/// Tools (function calling) rejimidagi javob: model yoki matn yozgan
/// (savol) yoki funksiyani chaqirgan (buyruq). Ikkalasi bir vaqtda bo'lmaydi.
/// </summary>
public class GeminiToolsResult
{
    public string? Text { get; init; }
    public GeminiToolCall? ToolCall { get; init; }
}

/// <summary>Gemini'ga e'lon qilinadigan funksiya (tool) deklaratsiyasi.</summary>
public class GeminiFunctionDeclaration
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;

    // JSON Schema ko'rinishidagi parametrlar (Dictionary<object> sifatida beriladi)
    [JsonPropertyName("parameters")] public object? Parameters { get; set; }
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
    private readonly ILogger<GeminiApiClient> _logger;

    public GeminiApiClient(HttpClient http, GeminiApiOptions options, ILogger<GeminiApiClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userMessage, int maxTokens = 1000, CancellationToken ct = default)
    {
        // Har bir muvaffaqiyatsiz urinishning tafsiloti shu ro'yxatga yoziladi — agar
        // barcha modellar ishlamasa, asl xato (status kod + Google xabari) final
        // exception'ning ichiga qo'yiladi va error sahifasida to'g'ridan-to'g'ri ko'rinadi.
        var diagnostics = new List<string>();

        // Avval ListModels orqali ishlaydigan modelni topamiz — birinchi urinishda
        // to'g'ri model bilan ishlaydi, 22s+ kechikish bo'lmaydi. ListModels
        // ishlamasa — fallback sifatida qo'lda ko'rsatilgan modelni sinaymiz.
        var auto = await TryAutoModelAsync(systemPrompt, userMessage, maxTokens, ct, diagnostics);
        if (auto != null) return auto;

        // Fallback: qo'lda ko'rsatilgan model yoki ishonchli defaultlar.
        var models = new List<string?>
            {
                _options.Model,
                "gemini-3.6-flash",
                "gemini-3.5-flash-lite",
                "gemini-2.5-flash",
                "gemini-2.0-flash",
            }
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        foreach (var model in models)
        {
            var text = await TryModelAsync(model, systemPrompt, userMessage, maxTokens, ct, diagnostics);
            if (text != null) return text;
        }

        var detail = diagnostics.Count == 0
            ? "(aniq xato yozilmadi — internet aloqasi uzilgan yoki so'rov time-out bo'lgan bo'lishi mumkin)"
            : string.Join(" | ", diagnostics);
        _logger.LogError("Gemini so'rovi bajarilmadi: barcha modellar mavjud emas yoki ruxsat etilmagan. Diagnostika: {Detail}", detail);
        throw new HttpRequestException($"Gemini so'rovi bajarilmadi: {detail}");
    }

    // Bitta modelga so'rov uchun maksimal kutish muddati. 8 soniya — sekin
    // modellarni tez skip qilish uchun yetarli (Gemini flash modellari odatda
    // 2-5s ichida javob beradi). 30s timeout ba'zi modellarda (masalan,
    // gemini-3.5-flash-lite) 20+ sekund cho'ziladi va foydalanuvchi kutib
    // qoladi — bu yerda uzunroq timeout foydasiz.
    private static readonly TimeSpan PerAttemptTimeout = TimeSpan.FromSeconds(8);

    /// <summary>Berilgan modelga urinadi; muvaffaqiyat bo'lsa matn, aks holda null qaytaradi.</summary>
    private async Task<string?> TryModelAsync(string model, string systemPrompt, string userMessage, int maxTokens, CancellationToken ct, List<string>? diagnostics = null)
    {
        try
        {
            var url = $"{_options.BaseUrl}/{model}:generateContent?key={_options.ApiKey}";
            var payload = BuildPayload(systemPrompt, userMessage, maxTokens);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(PerAttemptTimeout);

            var response = await _http.PostAsJsonAsync(url, payload, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var shortBody = errorBody.Length > 500 ? errorBody[..500] : errorBody;
                _logger.LogWarning("Gemini model={Model} status={Status} body={Body}", model, response.StatusCode, shortBody);
                diagnostics?.Add($"model={model} status={(int)response.StatusCode} body={shortBody}");
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: timeoutCts.Token);
            var text = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            return string.IsNullOrWhiteSpace(text) ? null : text!;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini model={Model} urinishi xato berdi", model);
            diagnostics?.Add($"model={model} exception={ex.Message}");
            return null;
        }
    }

    /// <summary>Google ListModels'dan generateContent'ga mos joriy modelni topib sinaydi.</summary>
    private async Task<string?> TryAutoModelAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct, List<string>? diagnostics = null)
    {
        try
        {
            var listUrl = $"{_options.BaseUrl}?key={_options.ApiKey}";
            var listResp = await _http.GetAsync(listUrl, ct);
            if (!listResp.IsSuccessStatusCode)
            {
                diagnostics?.Add($"ListModels status={(int)listResp.StatusCode}");
                return null;
            }

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

                    var shortName = name.Contains('/') ? name.Substring(name.LastIndexOf('/') + 1) : name;
                    var lower = shortName.ToLowerInvariant();
                    // Faqat tezkor (flash) chat modellarini olamiz — "pro"/"thinking"
                    // sekin va pullik, "tts"/"stt"/"image"/"video" mos emas.
                    if (lower.Contains("thinking")) continue;
                    if (lower.Contains("embedding")) continue;
                    if (lower.Contains("imagen")) continue;
                    if (lower.Contains("nano")) continue;
                    if (lower.Contains("pro")) continue;
                    if (lower.Contains("tts") || lower.Contains("stt")) continue;
                    if (lower.Contains("image") || lower.Contains("audio") || lower.Contains("video")) continue;
                    if (lower.Contains("preview")) continue;
                    if (!lower.Contains("flash") && !lower.Contains("lite")) continue;
                    candidates.Add(shortName);
                }
            }

            // Eng yaxshi tanlovlar: 2.x va 3.x flash modellarni birinchi o'ringa.
            candidates.Sort((a, b) => ScoreModel(a).CompareTo(ScoreModel(b)));

            // Eng yaxshi 3tasini sinaymiz — ko'proq urinish sekin modelga tushib
            // qolishi mumkin.
            foreach (var candidate in candidates.Take(3))
            {
                var text = await TryModelAsync(candidate, systemPrompt, userMessage, maxTokens, ct, diagnostics);
                if (text != null) return text;
            }
        }
        catch (Exception ex)
        {
            diagnostics?.Add($"ListModels exception={ex.Message}");
        }
        return null;
    }

    // ======================= Function calling (tools) =======================

    /// <summary>
    /// Tools (function calling) rejimida so'rov yuboradi: model buyruq deb
    /// topsa — funksiya chaqiruvini (ToolCall), savol deb topsa — matn javobini
    /// (Text) qaytaradi. Model tanlash mantig'i CompleteAsync bilan bir xil.
    /// </summary>
    public async Task<GeminiToolsResult> CompleteWithToolsAsync(
        string systemPrompt, string userMessage, IReadOnlyList<GeminiFunctionDeclaration> tools,
        int maxTokens = 1000, CancellationToken ct = default)
    {
        // Xuddi CompleteAsync'dagi kabi: har bir muvaffaqiyatsiz urinishning
        // tafsiloti (status + Google xabari) final exception'ga qo'shiladi.
        var diagnostics = new List<string>();

        // Avval ListModels orqali ishlaydigan flash modellarni topamiz.
        foreach (var model in await GetAutoCandidatesAsync(ct, diagnostics))
        {
            var result = await TryModelWithToolsAsync(model, systemPrompt, userMessage, tools, maxTokens, ct, diagnostics);
            if (result != null) return result;
        }

        // Fallback: qo'lda ko'rsatilgan model yoki ishonchli defaultlar.
        // Ro'yxat qisqa — eng yomon holatda ham so'rov 48s dan oshmaydi
        // (3 avto + 3 qo'lda × 8s), frontend baribir 12s da bekor qiladi.
        var models = new List<string?>
            {
                _options.Model,
                "gemini-3.6-flash",
                "gemini-3.5-flash-lite",
                "gemini-2.5-flash",
                "gemini-2.0-flash",
            }
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();

        foreach (var model in models)
        {
            var result = await TryModelWithToolsAsync(model, systemPrompt, userMessage, tools, maxTokens, ct, diagnostics);
            if (result != null) return result;
        }

        var detail = diagnostics.Count == 0
            ? "(aniq xato yozilmadi — internet aloqasi uzilgan yoki so'rov time-out bo'lgan bo'lishi mumkin)"
            : string.Join(" | ", diagnostics);
        _logger.LogError("Gemini tools so'rovi bajarilmadi: barcha modellar mavjud emas yoki ruxsat etilmagan. Diagnostika: {Detail}", detail);
        throw new HttpRequestException($"Gemini so'rovi bajarilmadi: {detail}");
    }

    private static GeminiRequest BuildToolPayload(
        string model, string systemPrompt, string userMessage, IReadOnlyList<GeminiFunctionDeclaration> tools, int maxTokens)
    {
        var payload = BuildPayload(systemPrompt, userMessage, maxTokens);
        // 2.5+ va 3.x modellarda "thinking" standart yoqil — token budjetini yeb,
        // javobni sekinlashtiradi yoki bo'sh qaytaradi. Tools rejimida o'chiriladi.
        // (Agar model thinkingConfig ni umuman qo'llab-quvvatlamasa, 400 qaytaradi —
        // TryModelWithToolsAsync bu holda thinking ni olib tashlab qayta urinadi.)
        payload.GenerationConfig.Thinking = new GeminiThinkingConfig { ThinkingBudget = 0 };
        payload.Tools = new List<GeminiToolContainer> { new() { FunctionDeclarations = tools.ToList() } };
        return payload;
    }

    /// <summary>Berilgan modelga tools bilan so'rov yuboradi; muvaffaqiyat bo'lsa natija, aks holda null.</summary>
    private async Task<GeminiToolsResult?> TryModelWithToolsAsync(
        string model, string systemPrompt, string userMessage,
        IReadOnlyList<GeminiFunctionDeclaration> tools, int maxTokens, CancellationToken ct, List<string>? diagnostics = null)
    {
        try
        {
            var url = $"{_options.BaseUrl}/{model}:generateContent?key={_options.ApiKey}";
            var payload = BuildToolPayload(model, systemPrompt, userMessage, tools, maxTokens);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(PerAttemptTimeout);

            var response = await _http.PostAsJsonAsync(url, payload, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                // Ba'zi modellar thinkingConfig ni umuman qo'llab-quvvatlamaydi va 400
                // "invalid argument" qaytaradi. Bu holda thinking ni olib tashlab
                // yana bir marta urinamiz.
                if ((int)response.StatusCode == 400)
                {
                    payload.GenerationConfig.Thinking = null;
                    response.Dispose();

                    using var retryCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    retryCts.CancelAfter(PerAttemptTimeout);
                    try
                    {
                        response = await _http.PostAsJsonAsync(url, payload, retryCts.Token);
                        if (response.IsSuccessStatusCode)
                        {
                            var r2 = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: retryCts.Token);
                            var parts2 = r2?.Candidates?.FirstOrDefault()?.Content?.Parts;
                            if (parts2 != null)
                            {
                                var call2 = parts2.FirstOrDefault(p => p.FunctionCall != null)?.FunctionCall;
                                if (call2 != null && !string.IsNullOrWhiteSpace(call2.Name))
                                {
                                    var argsJson2 = call2.Args.ValueKind == JsonValueKind.Object
                                        ? JsonSerializer.Serialize(call2.Args)
                                        : "{}";
                                    return new GeminiToolsResult { ToolCall = new GeminiToolCall { Name = call2.Name, ArgsJson = argsJson2 } };
                                }
                                var text2 = parts2.Select(p => p.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
                                if (!string.IsNullOrWhiteSpace(text2)) return new GeminiToolsResult { Text = text2 };
                            }
                        }
                        else
                        {
                            errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        return null;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Gemini tools model={Model} thinking-less qayta urinish xato berdi", model);
                        diagnostics?.Add($"model={model} retry exception={ex.Message}");
                        return null;
                    }
                }

                var shortBody = errorBody.Length > 500 ? errorBody[..500] : errorBody;
                _logger.LogWarning("Gemini tools model={Model} status={Status} body={Body}", model, response.StatusCode, shortBody);
                diagnostics?.Add($"model={model} status={(int)response.StatusCode} body={shortBody}");
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: timeoutCts.Token);
            var parts = result?.Candidates?.FirstOrDefault()?.Content?.Parts;
            if (parts == null) return null;

            // 1) Model funksiya chaqirdimi? (buyruq)
            var call = parts.FirstOrDefault(p => p.FunctionCall != null)?.FunctionCall;
            if (call != null && !string.IsNullOrWhiteSpace(call.Name))
            {
                var argsJson = call.Args.ValueKind == JsonValueKind.Object
                    ? JsonSerializer.Serialize(call.Args)
                    : "{}";
                return new GeminiToolsResult
                {
                    ToolCall = new GeminiToolCall { Name = call.Name, ArgsJson = argsJson },
                };
            }

            // 2) Model matn javob berdimi? (savol)
            var text = parts.Select(p => p.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
            return string.IsNullOrWhiteSpace(text)
                ? null
                : new GeminiToolsResult { Text = text };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini tools model={Model} urinishi xato berdi", model);
            diagnostics?.Add($"model={model} exception={ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// ListModels orqali generateContent'ni qo'llab-quvvatlaydigan tezkor
    /// (flash/lite) modellarni topadi — eng yaxshi 3tasi qaytadi. TryAutoModelAsync
    /// bilan bir xil saralash mantig'i (tools rejimi uchun alohida nusxa).
    /// </summary>
    private async Task<List<string>> GetAutoCandidatesAsync(CancellationToken ct, List<string>? diagnostics = null)
    {
        var candidates = new List<string>();
        try
        {
            var listUrl = $"{_options.BaseUrl}?key={_options.ApiKey}";
            var listResp = await _http.GetAsync(listUrl, ct);
            if (!listResp.IsSuccessStatusCode)
            {
                diagnostics?.Add($"ListModels status={(int)listResp.StatusCode}");
                return candidates;
            }

            var listRaw = await listResp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(listRaw);

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

                    var shortName = name.Contains('/') ? name.Substring(name.LastIndexOf('/') + 1) : name;
                    var lower = shortName.ToLowerInvariant();
                    // Sekin/pullik/mos bo'lmagan modellarni chiqarib tashlaymiz —
                    // faqat flash/lite chat modellari qoladi.
                    if (lower.Contains("thinking")) continue;
                    if (lower.Contains("embedding")) continue;
                    if (lower.Contains("imagen")) continue;
                    if (lower.Contains("nano")) continue;
                    if (lower.Contains("pro")) continue;
                    if (lower.Contains("tts") || lower.Contains("stt")) continue;
                    if (lower.Contains("image") || lower.Contains("audio") || lower.Contains("video")) continue;
                    if (lower.Contains("preview")) continue;
                    if (!lower.Contains("flash") && !lower.Contains("lite")) continue;
                    candidates.Add(shortName);
                }
            }

            // Eng yaxshi tanlovlar: 2.x va 3.x flash modellarni birinchi o'ringa.
            candidates.Sort((a, b) => ScoreModel(a).CompareTo(ScoreModel(b)));
        }
        catch (Exception ex)
        {
            diagnostics?.Add($"ListModels exception={ex.Message}");
        }
        return candidates.Take(3).ToList();
    }

    private static int ScoreModel(string name)
    {
        // Kichikroq = yaxshiroq tanlov. Yangi 3.x flash modellar ustuvor —
        // eski 2.x modellar yangi kalitlarda yopilgan (404 qaytaradi).
        var lower = name.ToLowerInvariant();
        if (lower.Contains("3.") && lower.Contains("flash")) return 0;
        if (lower.Contains("2.5") && lower.Contains("flash")) return 1;
        if (lower.Contains("2.") && lower.Contains("flash")) return 2;
        if (lower.Contains("lite")) return 3;
        if (lower.Contains("flash")) return 4;
        return 5;
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
            },
        };
    }

    private class GeminiRequest
    {
        [JsonPropertyName("system_instruction")] public GeminiContent SystemInstruction { get; set; } = new();
        [JsonPropertyName("contents")] public List<GeminiContent> Contents { get; set; } = new();
        [JsonPropertyName("generationConfig")] public GeminiGenerationConfig GenerationConfig { get; set; } = new();
        [JsonPropertyName("tools")] public List<GeminiToolContainer>? Tools { get; set; }
    }

    private class GeminiToolContainer
    {
        [JsonPropertyName("functionDeclarations")] public List<GeminiFunctionDeclaration> FunctionDeclarations { get; set; } = new();
    }

    private class GeminiGenerationConfig
    {
        [JsonPropertyName("maxOutputTokens")] public int MaxOutputTokens { get; set; }
        // Eslatma: "temperature" ataylab yuborilmaydi — uitni thinking/e'tiborli
        // modellar (2.5+, 3.x) 400 INVALID_ARGUMENT qaytaradi.

        // gemini-2.5-* modellarda "thinking" standart yoqil — maxOutputTokens
        // budjetini yeb qo'yadi va javob sekin/bo'sh chiqadi. Tools rejimida
        // o'chirib yuboriladi (thinkingBudget = 0).
        [JsonPropertyName("thinkingConfig")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiThinkingConfig? Thinking { get; set; }
    }

    private class GeminiThinkingConfig
    {
        [JsonPropertyName("thinkingBudget")] public int ThinkingBudget { get; set; }
    }

    private class GeminiContent
    {
        [JsonPropertyName("role")] public string? Role { get; set; }
        [JsonPropertyName("parts")] public List<GeminiPart> Parts { get; set; } = new();
    }

    private class GeminiPart
    {
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("functionCall")] public GeminiFunctionCallPayload? FunctionCall { get; set; }
    }

    private class GeminiFunctionCallPayload
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("args")] public JsonElement Args { get; set; }
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
