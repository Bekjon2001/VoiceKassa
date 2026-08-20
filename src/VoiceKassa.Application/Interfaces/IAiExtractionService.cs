using VoiceKassa.Application.DTOs;

namespace VoiceKassa.Application.Interfaces;

/// <summary>
/// Kassir/ofitsiant aytgan gapni strukturaviy JSON'ga aylantiradi.
/// Implementation lives in VoiceKassa.AiServices (calls the Gemini API).
/// </summary>
public interface IAiExtractionService
{
    Task<OrderExtractionResult> ExtractOrderAsync(string transcriptText, CancellationToken ct = default);
}
