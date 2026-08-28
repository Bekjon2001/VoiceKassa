namespace VoiceKassa.Application.Interfaces;

/// <summary>Do'kon/restoran egasining tabiiy tildagi savoliga javob beradi.</summary>
public interface IAiQueryService
{
    Task<string> AnswerAsync(string question, string dataContextJson, CancellationToken ct = default);
}
