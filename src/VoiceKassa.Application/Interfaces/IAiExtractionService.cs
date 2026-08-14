using VoiceKassa.Application.DTOs;

namespace VoiceKassa.Application.Interfaces;

/// <summary>
/// Turns a raw spoken/typed sentence into structured sale data.
/// Implementation lives in VoiceKassa.AiServices (calls the Gemini API).
/// </summary>
public interface IAiExtractionService
{
    Task<SaleExtractionResult> ExtractSaleAsync(string transcriptText, CancellationToken ct = default);
}
