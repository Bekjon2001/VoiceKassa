namespace VoiceKassa.Application.Interfaces;

/// <summary>Do'kon/restoran egasining tabiiy tildagi savoliga javob beradi.</summary>
public interface IAiQueryService
{
    Task<string> AnswerAsync(string question, string dataContextJson, CancellationToken ct = default);

    /// <summary>Super Admin platforma darajasidagi savoliga (barcha bizneslar/obunalar haqida) javob beradi.</summary>
    Task<string> AnswerPlatformAsync(string question, string dataContextJson, CancellationToken ct = default);
}
