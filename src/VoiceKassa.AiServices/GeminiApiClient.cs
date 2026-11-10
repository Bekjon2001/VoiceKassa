using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace VoiceKassa.AiServices;

public class GeminiApiOptions
{
    public string ApiKey { get; set; } = string.Empty;

    // Bitta model nomi. Auto-detect (ListModels) o'chirilgan — shu model
    // 1 soat xotirada keshlanadi, har so'rovda ListModels chaqirmaydi.
    public string Model { get; set; } = "gemini-2.0-flash";
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

    // Model xotirada keshlash (1 soat). Har so'rovda ListModels chaqirmaydi.
    private string? _cachedModel;
    private DateTime _cachedModelExpiry = DateTime.MinValue;

    public GeminiApiClient(HttpClient http, GeminiApiOptions options, ILogger<GeminiApiClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userMessage, int maxTokens = 1000, CancellationToken ct = default)
    {
        var model = await GetCachedModelAsync(ct);

        var url = $"{_options.BaseUrl}/{model}:generateContent";
        var payload = BuildPayload(systemPrompt, userMessage, maxTokens);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-goog-api-key", _options.ApiKey);
        request.Content = JsonContent.Create(payload);

        try
        {
            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Gemini model={Model} status={Status} body={Body}", model, response.StatusCode, errorBody);
                throw new HttpRequestException("Gemini so'rovi bajarilmadi.");
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: ct);
            var text = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            return string.IsNullOrWhiteSpace(text)
                ? throw new HttpRequestException("Gemini so'rovi bajarilmadi.")
                : text!;
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini model={Model} xato", model);
            throw new HttpRequestException("Gemini so'rovi bajarilmadi.");
        }
    }

    private async Task<string> GetCachedModelAsync(CancellationToken ct)
    {
        if (_cachedModel is not null && DateTime.UtcNow < _cachedModelExpiry)
            return _cachedModel;

        var model = string.IsNullOrWhiteSpace(_options.Model) ? "gemini-2.0-flash" : _options.Model;
        _cachedModel = model;
        _cachedModelExpiry = DateTime.UtcNow.AddHours(1);
        return model;
    }

    public async Task<GeminiToolsResult> CompleteWithToolsAsync(string systemPrompt, string userMessage, List<GeminiFunctionDeclaration> tools, int maxTokens = 1000, CancellationToken ct = default)
    {
        var model = await GetCachedModelAsync(ct);

        var url = $"{_options.BaseUrl}/{model}:generateContent";
        var payload = BuildPayload(systemPrompt, userMessage, maxTokens);
        payload.Tools = new List<GeminiToolContainer> { new() { FunctionDeclarations = tools } };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-goog-api-key", _options.ApiKey);
        request.Content = JsonContent.Create(payload);

        try
        {
            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Gemini model={Model} status={Status} body={Body}", model, response.StatusCode, errorBody);
                throw new HttpRequestException("Gemini so'rovi bajarilmadi.");
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: ct);
            var text = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            var functionCall = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.FunctionCall;

            if (functionCall is not null)
            {
                return new GeminiToolsResult
                {
                    ToolCall = new GeminiToolCall
                    {
                        Name = functionCall.Name,
                        ArgsJson = functionCall.Args.GetRawText(),
                    }
                };
            }

            return new GeminiToolsResult
            {
                Text = string.IsNullOrWhiteSpace(text) ? null : text,
            };
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini model={Model} xato", model);
            throw new HttpRequestException("Gemini so'rovi bajarilmadi.");
        }
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