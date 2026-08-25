using VoiceKassa.Application.DTOs;

namespace VoiceKassa.Application.Interfaces;

/// <summary>
<<<<<<< HEAD
/// Turns a raw spoken/typed sentence into structured sale data.
=======
/// Kassir/ofitsiant aytgan gapni strukturaviy JSON'ga aylantiradi.
>>>>>>> main
/// Implementation lives in VoiceKassa.AiServices (calls the Gemini API).
/// </summary>
public interface IAiExtractionService
{
<<<<<<< HEAD
    Task<SaleExtractionResult> ExtractSaleAsync(string transcriptText, CancellationToken ct = default);
=======
    Task<OrderExtractionResult> ExtractOrderAsync(string transcriptText, CancellationToken ct = default);
>>>>>>> main
}
